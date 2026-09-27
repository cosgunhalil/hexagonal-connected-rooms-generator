using System.Text;
using CRG.Building;
using CRG.Generation;

namespace CRG.Tests
{
    // Recipes for the pinned hand-built levels of HandBuiltGoldenTests: a seeded random layout of every shape,
    // shared edges set to open, door or wall, and per config the level settings and room settings.
    public static class HandBuiltGoldenLayouts
    {
        public static readonly string[] Configs = { "handBuilt", "handBuiltThickCeilings", "handBuiltThickFullDoors" };

        public const int Seeds = 5;

        public static GenerationParameters CreateParameters(string config)
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            switch (config)
            {
                case "handBuiltThickCeilings":
                    parameters.WallThickness = 1f;
                    parameters.AddCeiling = true;
                    break;
                case "handBuiltThickFullDoors":
                    parameters.WallThickness = 2f;
                    parameters.DoorHeight = 3f;
                    parameters.DoorWidthRatio = 0.15f;
                    break;
            }
            return parameters;
        }

        public static LevelLayout CreateLayout(string config, int seed)
        {
            System.Random random = new System.Random(seed);
            LevelLayout layout = new LevelLayout();
            layout.AddFirstCell(CellShapes.All[random.Next(CellShapes.All.Length)]);

            for (int attempt = 0; attempt < 400 && layout.Cells.Count < 25; attempt++)
            {
                LayoutCell cell = layout.Cells[random.Next(layout.Cells.Count)];
                layout.Attach(cell.id, random.Next(CellShapes.GetEdgeCount(cell.shape)), CellShapes.All[random.Next(CellShapes.All.Length)]);
            }

            foreach (LayoutLink link in layout.Links)
            {
                double roll = random.NextDouble();
                link.state = roll < 0.5 ? EdgeState.Open : roll < 0.75 ? EdgeState.Door : EdgeState.Wall;
            }

            if (config == "handBuiltThickCeilings")
            {
                LayoutRooms rooms = layout.GetRooms();
                for (int room = 0; room < rooms.Rooms.Count; room++)
                    rooms.Settings[room].roomCeiling = (RoomCeiling)(room % 3);
            }

            return layout;
        }

        // Discrete data only (shapes, links, ceilings): placements are doubles from trigonometry and may differ in
        // the last bit between runtimes; the geometry signature covers where cells are.
        public static string Fingerprint(LevelLayout layout, GenerationParameters parameters)
        {
            StringBuilder builder = new StringBuilder();
            foreach (LayoutCell cell in layout.Cells)
                builder.Append(cell.id).Append(':').Append(cell.shape).Append(';');
            builder.Append('\n');

            foreach (LayoutLink link in layout.Links)
            {
                builder.Append(link.cellA).Append('/').Append(link.edgeA).Append('-').Append(link.cellB).Append('/').Append(link.edgeB)
                    .Append(':').Append(link.state).Append(';');
            }
            builder.Append('\n');

            LayoutRooms rooms = layout.GetRooms();
            for (int room = 0; room < rooms.Rooms.Count; room++)
                builder.Append(string.Join(",", rooms.Rooms[room])).Append(rooms.HasCeiling(room, parameters.AddCeiling) ? "C" : "O").Append(';');

            return builder.ToString();
        }

        public static ulong Fnv(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
