# Security Policy

## Supported versions

CRG is in `0.x` development, and only the latest release receives fixes. Please update to it before reporting a problem.

| Version | Supported |
|---|---|
| 0.6.x | Yes |
| < 0.6 | No |

## Reporting a vulnerability

**Please do not report security problems in public issues, discussions or pull requests.**

Report them privately through GitHub instead: open the repository's **Security** tab and click **Report a vulnerability**. Include:

- what the problem is and where it is (file, class or menu);
- the steps to reproduce it, with the Unity version and generation settings if they matter;
- what an attacker could do with it.

You can expect a first answer within a week. Once the problem is confirmed, a fix is released as soon as possible, and you are credited in the release notes unless you prefer otherwise. Please keep the problem private until the fix is released.

## Scope

CRG runs inside the Unity Editor and in player builds. Examples of problems worth reporting:

- the Editor tools writing, overwriting or deleting files outside the places they are meant to (for example the NavMesh asset clean-up);
- generation settings from a scene or asset making the Editor or a player build hang or crash.

Bugs in Unity, ProBuilder or AI Navigation themselves should be reported to Unity.
