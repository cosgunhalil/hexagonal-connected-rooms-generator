using System;
using System.Collections.Generic;
using System.Linq;
using CRG.Core;
using UnityEngine;

namespace CRG.Generation
{
    // Builds levels whose rooms use different grid types. Every grid has the same edge length, so any outer edge
    // of one room can be matched exactly by an outer edge of a room on any other grid:
    // 1. The seed room is grown at the origin on a weighted-random grid type.
    // 2. A free outer edge of a placed room is picked (Layout Bias prefers edges near or far from the origin).
    // 3. A new room is grown on its own grid, rotated and moved so one of its outer edges lies exactly on the
    //    picked edge (in the opposite direction), and kept only if it overlaps no room and touches other rooms
    //    only along exactly matching edges. The picked edge becomes a door.
    // 4. Other exactly shared edges become walls between rooms, or extra doors by Loop Chance.
    public class MixedLevelGenerator
    {
        private const int MaxLayoutCandidates = 8;
        private const float ToleranceRatio = 1e-3f;
        private const int MaxFailuresPerRolledType = 8;

        private static GridType[] MixableTypes => GridTopology.SingleGridTypes;

        private GenerationParameters parameters;
        private System.Random random;
        private RoomShapeGenerator shapeGenerator;
        private MixedLevel level;
        private float tolerance;
        private SpatialHash<PlacedCell> cells;
        private SpatialHash<OpenEdge> openEdges;
        private List<OpenEdge> frontier;
        private GridType? pendingType;
        private int pendingTypeFailures;

        private class PlacedCell
        {
            public int RoomID;
            public Vector3[] Corners;
        }

        private class OpenEdge
        {
            public int RoomID;
            public CellCoord Cell;
            public int Edge;
            public Vector3 Start;
            public Vector3 End;
            public Vector3 Middle => (Start + End) / 2f;
        }

        public MixedLevel Generate(GenerationParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            if (!parameters.Validate(out string errorMessage))
                throw new ArgumentException($"Invalid parameters: {errorMessage}");

            if (TotalWeight(parameters) <= 0f)
                throw new ArgumentException("Invalid parameters: at least one grid type weight must be above 0");

            this.parameters = parameters;
            random = parameters.RandomSeed >= 0 ? new System.Random(parameters.RandomSeed) : new System.Random();
            shapeGenerator = new RoomShapeGenerator();
            level = new MixedLevel(parameters.CellSize);
            tolerance = parameters.CellSize * ToleranceRatio;
            cells = new SpatialHash<PlacedCell>(parameters.CellSize * 3f);
            openEdges = new SpatialHash<OpenEdge>(parameters.CellSize * 3f);
            frontier = new List<OpenEdge>();
            pendingType = null;
            pendingTypeFailures = 0;

            GridType seedType = ChooseGridType();
            PlacedRoom seed = null;
            for (int attempt = 0; attempt < parameters.MaxRetriesPerRoom && seed == null; attempt++)
            {
                seed = CreateCandidate(seedType);
            }

            if (seed == null)
            {
                Debug.LogError("Failed to create the seed room of the mixed level");
                return level;
            }

            Commit(seed, RoomPlacement.Identity, new List<(OpenEdge, CellCoord, int)>(), null);

            int iterations = 0;
            while (level.Rooms.Count < parameters.TargetRoomCount && iterations < parameters.MaxIterations && frontier.Count > 0)
            {
                iterations++;
                OpenEdge target = SelectFromFrontier();

                if (!HasCapacity(level.GetRoom(target.RoomID)) || !TryAttach(target))
                {
                    frontier.Remove(target);
                }
            }

            EnsureMinimumConnections();
            AssignCeilings();

            Debug.Log($"Mixed level complete: {level.Rooms.Count} rooms in {iterations} iterations " +
                      $"({string.Join(", ", level.Rooms.GroupBy(r => r.GridType).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"))}), " +
                      $"{level.Doors.Count()} doors, {level.Links.Count(link => !link.IsDoor)} walls between rooms");

            return level;
        }

        private PlacedRoom CreateCandidate(GridType type)
        {
            IGridTopology topology = GridTopology.Get(type);
            CellGrid grid = new CellGrid(topology, parameters.CellSize);

            // Starting from the origin or one of its neighbors lets grids with two cell shapes start from either.
            CellCoord start = new CellCoord(0, 0);
            if (random.NextDouble() < 0.5)
                start = topology.GetNeighbor(start, random.Next(topology.GetEdgeCount(start)));

            int size = random.Next(parameters.MinCellsPerRoom, parameters.MaxCellsPerRoom + 1);
            List<CellCoord> roomCells = shapeGenerator.GenerateRoomShapeWithRetry(
                grid, start, size, parameters.MinCellsPerRoom, parameters.MaxRetriesPerRoom, random);

            if (roomCells.Count < parameters.MinCellsPerRoom)
                return null;

            Room room = grid.CreateRoom();
            foreach (CellCoord cell in roomCells)
                grid.AssignCellToRoom(cell, room.RoomID);

            new ConnectionAnalyzer(parameters.MaxConnectionsPerRoom).FlagRoomEdges(grid, room);
            return new PlacedRoom(type, grid, room);
        }

        private GridType ChooseGridType()
        {
            double pick = random.NextDouble() * TotalWeight(parameters);
            foreach (GridType type in MixableTypes)
            {
                float weight = GetWeight(parameters, type);
                if (weight <= 0f)
                    continue;
                if (pick < weight)
                    return type;
                pick -= weight;
            }

            return MixableTypes.Last(type => GetWeight(parameters, type) > 0f);
        }

        public static float GetWeight(GenerationParameters parameters, GridType type)
        {
            return parameters.GetGridWeight(type);
        }

        private static float TotalWeight(GenerationParameters parameters)
        {
            return parameters.GetTotalGridWeight();
        }

        // A rolled grid type is kept across frontier edges until a room of that type is placed (or it failed on
        // several edges), so placement failures, which are more common for large cells, do not quietly shift the
        // level's mix of grid types away from the weights.
        private bool TryAttach(OpenEdge target)
        {
            if (pendingType == null || pendingTypeFailures >= MaxFailuresPerRolledType)
            {
                pendingType = ChooseGridType();
                pendingTypeFailures = 0;
            }

            GridType type = pendingType.Value;

            for (int attempt = 0; attempt < parameters.MaxRetriesPerRoom; attempt++)
            {
                PlacedRoom candidate = CreateCandidate(type);
                if (candidate == null)
                    continue;

                List<(CellCoord cell, int edge)> outerEdges = candidate.GetOuterEdges().ToList();
                Shuffle(outerEdges);

                foreach ((CellCoord cell, int edge) in outerEdges)
                {
                    (Vector3 localStart, Vector3 localEnd) = candidate.GetLocalEdge(cell, edge);
                    // The shared edge runs the other way in the new room.
                    RoomPlacement placement = RoomPlacement.Align(localStart, localEnd, target.End, target.Start);

                    // The target edge must be among the matched edges, or the door would have nowhere to go.
                    if (Fits(candidate, placement, out List<(OpenEdge, CellCoord, int)> contacts) &&
                        contacts.Any(contact => contact.Item1 == target && contact.Item2 == cell && contact.Item3 == edge))
                    {
                        Commit(candidate, placement, contacts, (target, cell, edge));
                        pendingType = null;
                        return true;
                    }
                }
            }

            pendingTypeFailures++;
            return false;
        }

        // The candidate fits when none of its cells overlaps a placed cell and each of its outer edges either
        // touches nothing or matches a free outer edge of a placed room exactly.
        private bool Fits(PlacedRoom candidate, RoomPlacement placement, out List<(OpenEdge, CellCoord, int)> contacts)
        {
            contacts = new List<(OpenEdge, CellCoord, int)>();
            candidate.Placement = placement;

            foreach (CellCoord cell in candidate.Room.Cells)
            {
                Vector3[] corners = candidate.GetWorldCorners(cell);
                Vector3 center = candidate.GetWorldCenter(cell);

                foreach (PlacedCell placed in cells.Query(center))
                {
                    if (PlanarGeometry.ConvexPolygonsOverlap(corners, placed.Corners, tolerance))
                        return false;
                }
            }

            foreach ((CellCoord cell, int edge) in candidate.GetOuterEdges())
            {
                (Vector3 start, Vector3 end) = candidate.GetWorldEdge(cell, edge);

                foreach (OpenEdge open in openEdges.Query((start + end) / 2f))
                {
                    switch (PlanarGeometry.GetContact(start, end, open.Start, open.End, tolerance))
                    {
                        case PlanarGeometry.SegmentContact.ExactOpposite:
                            contacts.Add((open, cell, edge));
                            break;
                        case PlanarGeometry.SegmentContact.PartialOverlap:
                            return false;
                    }
                }
            }

            return true;
        }

        private void Commit(PlacedRoom room, RoomPlacement placement, List<(OpenEdge open, CellCoord cell, int edge)> contacts,
            (OpenEdge open, CellCoord cell, int edge)? door)
        {
            room.RoomID = level.Rooms.Count;
            room.Placement = placement;
            level.Rooms.Add(room);

            foreach (CellCoord cell in room.Room.Cells)
            {
                cells.Add(room.GetWorldCenter(cell), new PlacedCell { RoomID = room.RoomID, Corners = room.GetWorldCorners(cell) });
            }

            HashSet<(CellCoord, int)> linkedEdges = new HashSet<(CellCoord, int)>();
            Dictionary<int, List<RoomLink>> linksByRoom = new Dictionary<int, List<RoomLink>>();

            foreach ((OpenEdge open, CellCoord cell, int edge) in contacts)
            {
                RoomLink link = new RoomLink
                {
                    RoomA = open.RoomID, CellA = open.Cell, EdgeA = open.Edge,
                    RoomB = room.RoomID, CellB = cell, EdgeB = edge
                };
                level.Links.Add(link);
                linkedEdges.Add((cell, edge));
                openEdges.Remove(open.Middle, open);
                frontier.Remove(open);

                if (!linksByRoom.TryGetValue(open.RoomID, out List<RoomLink> list))
                    linksByRoom[open.RoomID] = list = new List<RoomLink>();
                list.Add(link);

                if (door.HasValue && door.Value.open == open)
                    MakeDoor(link);
            }

            // Extra doors to other rooms this one happens to touch, as in single-grid levels.
            int maxNewConnections = parameters.ReserveConnectionForGrowth
                ? Math.Max(1, parameters.MaxConnectionsPerRoom - 1)
                : parameters.MaxConnectionsPerRoom;

            foreach (int otherID in linksByRoom.Keys.OrderBy(id => id))
            {
                PlacedRoom other = level.GetRoom(otherID);
                if (room.Doors.ContainsKey(otherID) || room.ConnectionCount >= maxNewConnections || !HasCapacity(other))
                    continue;

                if (parameters.LoopChance < 1f && random.NextDouble() >= parameters.LoopChance)
                    continue;

                List<RoomLink> options = linksByRoom[otherID];
                MakeDoor(options[random.Next(options.Count)]);
            }

            foreach ((CellCoord cell, int edge) in room.GetOuterEdges())
            {
                if (linkedEdges.Contains((cell, edge)))
                    continue;

                (Vector3 start, Vector3 end) = room.GetWorldEdge(cell, edge);
                OpenEdge open = new OpenEdge { RoomID = room.RoomID, Cell = cell, Edge = edge, Start = start, End = end };
                openEdges.Add(open.Middle, open);
                frontier.Add(open);
            }
        }

        private void MakeDoor(RoomLink link)
        {
            link.IsDoor = true;
            PlacedRoom a = level.GetRoom(link.RoomA);
            PlacedRoom b = level.GetRoom(link.RoomB);
            a.Grid.GetCell(link.CellA).SetEdgeFlag(link.EdgeA, WallFlag.HasDoor);
            b.Grid.GetCell(link.CellB).SetEdgeFlag(link.EdgeB, WallFlag.HasDoor);
            a.Doors[b.RoomID] = link;
            b.Doors[a.RoomID] = link;
        }

        private bool HasCapacity(PlacedRoom room)
        {
            return room.ConnectionCount < parameters.MaxConnectionsPerRoom;
        }

        // After the layout is final, as in single-grid levels; at CeilingChance 1 no random numbers are drawn.
        private void AssignCeilings()
        {
            if (!parameters.AddCeiling || parameters.CeilingChance >= 1f)
                return;

            foreach (PlacedRoom room in level.Rooms)
                room.Room.HasCeiling = random.NextDouble() < parameters.CeilingChance;
        }

        // Rooms placed early may end up below MinConnectionsPerRoom; turn shared walls into doors where limits allow.
        private void EnsureMinimumConnections()
        {
            foreach (PlacedRoom room in level.Rooms)
            {
                foreach (RoomLink link in level.GetLinks(room.RoomID).Where(l => !l.IsDoor).ToList())
                {
                    if (room.ConnectionCount >= parameters.MinConnectionsPerRoom)
                        break;

                    PlacedRoom other = level.GetRoom(link.GetOther(room.RoomID));
                    if (!room.Doors.ContainsKey(other.RoomID) && HasCapacity(room) && HasCapacity(other))
                        MakeDoor(link);
                }
            }
        }

        // Tournament selection as in single-grid levels: LayoutBias sets how many random frontier edges compete,
        // and the one farthest from (bias > 0) or closest to (bias < 0) the origin wins.
        private OpenEdge SelectFromFrontier()
        {
            OpenEdge best = frontier[random.Next(frontier.Count)];
            int candidates = 1 + (int)Math.Round(Math.Abs(parameters.LayoutBias) * (MaxLayoutCandidates - 1));
            bool preferFar = parameters.LayoutBias > 0f;

            for (int i = 1; i < candidates; i++)
            {
                OpenEdge candidate = frontier[random.Next(frontier.Count)];
                float candidateDistance = candidate.Middle.magnitude;
                float bestDistance = best.Middle.magnitude;
                if (preferFar ? candidateDistance > bestDistance : candidateDistance < bestDistance)
                    best = candidate;
            }

            return best;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Buckets items by position so overlap and contact tests only look at nearby cells and edges. The bucket
        // size must exceed twice the largest distance between an item's anchor and any point of it.
        private class SpatialHash<T>
        {
            private readonly float bucketSize;
            private readonly Dictionary<(int, int), List<T>> buckets = new Dictionary<(int, int), List<T>>();

            public SpatialHash(float bucketSize)
            {
                this.bucketSize = bucketSize;
            }

            public void Add(Vector3 position, T item)
            {
                (int, int) key = Key(position);
                if (!buckets.TryGetValue(key, out List<T> list))
                    buckets[key] = list = new List<T>();
                list.Add(item);
            }

            public void Remove(Vector3 position, T item)
            {
                if (buckets.TryGetValue(Key(position), out List<T> list))
                    list.Remove(item);
            }

            public IEnumerable<T> Query(Vector3 position)
            {
                (int x, int z) = Key(position);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (buckets.TryGetValue((x + dx, z + dz), out List<T> list))
                        {
                            foreach (T item in list)
                                yield return item;
                        }
                    }
                }
            }

            private (int, int) Key(Vector3 position)
            {
                return (Mathf.FloorToInt(position.x / bucketSize), Mathf.FloorToInt(position.z / bucketSize));
            }
        }
    }
}
