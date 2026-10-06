---
name: realistic-engine-sounds-2
description: >-
  Helps AI assistants integrate Realistic Engine Sounds 2 (RES2) with
  Unity vehicle controllers and configure RES2 using its provided
  compatibility packages, engine sound prefabs, and audio effect prefabs.
  Use when setting up RES2 on a vehicle, selecting engine sounds,
  configuring interior or exterior engine audio, or adding RES2 audio
  effects such as turbo, supercharger, muffler crackle, gear shifting,
  or straight cut gearbox.
---

# Realistic Engine Sounds 2

## Purpose

Use this skill when working with Realistic Engine Sounds 2 (RES2),
especially when integrating RES2 with a Unity vehicle controller or
configuring RES2 engine and audio effect prefabs.

RES2 is designed to use pre-made compatibility integrations and
pre-made audio prefabs.

Prefer the existing RES2 prefabs, compatibility packages, scripts,
and documentation over recreating their functionality.

---

## Vehicle Controller Compatibility

RES2 vehicle controller compatibility integrations are located in:

`Assets/Assets_For_Vehicle_Controllers/`

Each supported vehicle controller has its own folder.

The folder name is a compatibility identifier and may not exactly
match the full name of the vehicle controller.

For example:

- Realistic Car Controller → `RCC`
- NWH Vehicle Physics 2 → `NWH2`

Do not assume that a compatibility folder name can always be derived
directly from the vehicle controller's full name.

### Finding a Compatibility

When setting up RES2 on a vehicle:

1. Identify the vehicle controller used by the vehicle.
2. Inspect `Assets/Assets_For_Vehicle_Controllers/`.
3. Find the corresponding compatibility folder.
4. Read the `ReadMe` file provided in that folder.
5. Follow the installation and integration instructions in that
   ReadMe.
6. Import the provided `.unitypackage` when required.
7. Use the compatibility prefab and scripts provided by the package.

The compatibility ReadMe is the primary source for
vehicle-controller-specific installation instructions.

Do not assume that all vehicle controller integrations use the same
installation procedure.

### Custom Vehicle Controllers

If no dedicated compatibility integration exists for the vehicle
controller, check:

`Assets/Assets_For_Vehicle_Controllers/CustomCarController/`

This folder contains an example compatibility setup for custom or
otherwise unsupported vehicle controllers.

The example compatibility script is intended to work with many
vehicle controllers with minimal modifications.

Read the `CustomCarController` ReadMe and use the provided example
as the starting point for a custom integration.

Do not assume that the example works without modification.

---

## RES2 Prefab Hierarchy

After the vehicle-controller compatibility has been installed, RES2
uses the following hierarchy:

Vehicle
└── Compatibility Prefab
    ├── Exterior Engine Sound [optional]
    │   └── Additional Effects [optional]
    └── Interior Engine Sound [optional]
        └── Additional Effects [optional]

A compatibility prefab can contain:

- an exterior Engine Sound only
- an interior Engine Sound only
- both an exterior and an interior Engine Sound

A maximum of two Engine Sound prefabs should be placed under the
compatibility prefab.

If both exterior and interior Engine Sound prefabs are used, they
must represent the same engine sound.

Do not combine unrelated engine sounds.

---

## Engine Sound Prefabs

Use the existing RES2 Engine Sound prefabs.

Do not create replacement Engine Sound prefabs when an appropriate
RES2 prefab already exists.

Engine Sound prefabs are organized by quality level.

The main quality folders are:

`Assets/Prefabs/Engine_Prefabs/High Quality/`

`Assets/Prefabs/Engine_Prefabs/Medium Quality/`

`Assets/Prefabs/Engine_Prefabs/Low Quality/`

Corresponding interior prefabs are located inside:

`High Quality/High_Interior/`

`Medium Quality/Medium_Interior/`

`Low Quality/Low_Interior/`

When selecting an Engine Sound:

1. Choose the appropriate quality level.
2. Inspect the actual prefabs available in that folder.
3. Select an Engine Sound appropriate for the vehicle.
4. If an interior sound is required, select the corresponding
   interior variant from the matching Interior folder.
5. Do not invent or generate prefab names.

The available prefab list may change between RES2 versions. Always
use the prefabs actually present in the installed RES2 package.

### Exterior and Interior Matching

If both exterior and interior Engine Sound prefabs are used, they
must represent the same engine sound.

For example:

`i6_German_HQ`

should be paired with its corresponding interior variant from:

`High Quality/High_Interior/`

Do not assume that the interior prefab name can always be generated
by modifying the exterior prefab name.

Inspect the actual Interior folder and select the matching prefab.

---

## Additional Audio Effects

Additional RES2 audio effect prefabs can be added as children of an
Engine Sound prefab.

Examples include:

- Muffler Crackle
- Gear Shifting
- Turbo
- Supercharger
- Straight Cut Gearbox

Correct:

Vehicle
└── Compatibility
    └── Engine Sound
        ├── Turbo
        ├── Supercharger
        ├── Muffler Crackle
        ├── Gear Shifting
        └── Straight Cut Gearbox

Do not place these effect prefabs directly under the vehicle or
directly under the compatibility prefab.

---

## Interior Audio Effects

When an audio effect provides a dedicated Interior variant, use the
Interior variant for the Interior Engine Sound.

Inspect the effect's available folders and select the appropriate
Interior prefab when one exists.

Do not use an exterior effect prefab for an interior Engine Sound
when a dedicated Interior version is available.

---

## Prefab Selection

When selecting RES2 prefabs:

1. Inspect the available folders and prefabs.
2. Select from the prefabs actually present in the installed
   RES2 package.
3. Choose the Engine Sound appropriate for the vehicle.
4. Match exterior and interior Engine Sound variants when both are
   used.
5. Select appropriate audio effects for the Engine Sound.
6. Use Interior effect variants for Interior Engine Sounds when
   available.
7. Preserve the existing RES2 prefab hierarchy.

Do not invent prefab names.

Do not create replacement prefabs when an appropriate existing RES2
prefab is available.

---

## Do Not Recreate Existing RES2 Functionality

When RES2 already provides a compatibility prefab, script, or audio
prefab for the requested functionality, use the provided asset.

Do not:

- recreate the RES2 engine controller
- recreate RES2 engine audio logic
- create replacement Engine Sound prefabs
- recreate a compatibility prefab
- write custom integration code when a dedicated compatibility exists
- invent compatibility prefabs
- invent RES2 prefab names

Custom integration code may be created when no dedicated compatibility
exists, using the `CustomCarController` example as the starting point.

---

## Unsupported Vehicle Controllers

If a vehicle controller does not have a dedicated RES2 compatibility:

1. Check `CustomCarController`.
2. Read its ReadMe.
3. Use the provided example compatibility script as the starting
   point.
4. Adapt it to the vehicle controller as required.

Do not assume that another vehicle controller's compatibility is
compatible with the unsupported controller.

---

## General Rule

Prefer this workflow:

Identify vehicle controller
→ Find compatibility folder
→ Read compatibility ReadMe
→ Import compatibility package if required
→ Use compatibility prefab
→ Select Engine Sound prefab
→ Optionally add matching Interior Engine Sound
→ Add appropriate RES2 effect prefabs
→ Preserve the RES2 prefab hierarchy