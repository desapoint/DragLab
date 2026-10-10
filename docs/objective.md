# 2D Drag Racing Game — First Iteration

## 1. Core Vision

A progression-focused 2D drag racing game combining:

- Accessible racing controls.
- Realistic-enough vehicle physics.
- Deep vehicle upgrading and tuning.
- Career progression.
- Quick/free racing.
- Offline/idle income.
- Vehicle collection.
- Visual customization.
- Performance classes from **F to S**.
- Real-world-inspired engine, drivetrain, gearing, tire, weight, and performance data.
- Optional manual driving mechanics for players who want greater control.
- Difficulty settings that affect both driving assists and opponent quality.

The initial release focuses entirely on straight-line drag racing.

Future game modes such as circuit racing, drifting, highway pulls, roll racing, time attack, and multiplayer should be considered architecturally, but are **not part of V1**.

---

# 2. Main Game Loop

The central progression loop is:

**Earn → Buy → Upgrade → Tune → Race → Win → Unlock → Invest → Repeat**

More specifically:

1. Player receives or buys a starter car.
2. Player races in Career or Free Race.
3. Player earns:
   - Cash
   - XP
   - Reputation
   - Parts
   - Occasionally cars or special rewards
4. Player upgrades the car.
5. Player tunes:
   - Gearing
   - Tire pressure
   - Launch RPM
   - Shift RPM
   - Differential
   - Boost settings where applicable
6. Car becomes faster but may also become harder to drive.
7. Player enters higher-performance events.
8. New vehicles, parts, race locations, and upgrade tiers unlock.
9. Player's garage produces some passive income while offline.
10. Player builds additional cars for different classes and event restrictions.

A secondary loop is:

**Collect → Customize → Optimize → Compare**

Cars should feel sufficiently different that owning multiple vehicles is useful rather than simply buying one car and upgrading it forever.

---

# 3. Game Modes

## Career

Career is the primary structured game mode.

The player progresses through racing tiers:

- F Class
- E Class
- D Class
- C Class
- B Class
- A Class
- S Class

Each tier contains:

- Normal races
- Qualification races
- Rival races
- Manufacturer challenges
- Restricted races
- Boss/champion races
- Prize-car events
- Upgrade-limited events

Example restrictions:

- FWD only
- RWD only
- AWD only
- Naturally aspirated
- Turbo only
- Under 2,000 cc
- Under 1,200 kg
- Production engine
- Street tires
- Maximum C rating
- Maximum 350 HP
- Minimum vehicle weight
- Specific manufacturer
- Specific vehicle generation

This encourages keeping multiple cars.

---

## Free Race

Free Race allows the player to configure:

- Distance
- Opponent
- Opponent difficulty
- Opponent performance
- Track
- Weather if implemented
- Time of day
- Transmission mode
- Driving assists
- Starting procedure

Recommended race distances:

- 60 ft
- 1/8 mile
- 1/4 mile
- 1/2 mile
- 1 mile

The 1/4 mile should be the primary balancing distance.

Free Race gives reduced rewards compared with Career to prevent farming exploits.

---

# 4. Race Sequence

A race should consist of more than simply tapping shift.

## Stage 1 — Preparation

Before staging:

- Choose tune.
- Choose tires.
- Adjust tire pressure.
- Adjust launch RPM.
- Enable/disable assists.
- Optionally perform a burnout.

A burnout can affect:

- Tire temperature
- Tire grip
- Tire wear

---

## Stage 2 — Staging

The player approaches the starting line.

Optional staging depth:

- Pre-stage light
- Stage light
- Deep staging
- Automatic staging assist

More advanced players can stage manually.

---

## Stage 3 — Launch

Lights run through the starting sequence.

Player can configure:

### Automatic Launch

The game controls:

- Clutch
- Initial throttle
- Launch RPM
- Initial gear engagement

The player mainly reacts to the lights.

### Assisted Launch

Player controls throttle while the game manages clutch engagement.

### Manual Launch

Player controls:

- Throttle
- RPM
- Clutch engagement
- Gear engagement

Poor technique can cause:

- Bogging
- Excessive wheelspin
- Clutch slip
- Engine hitting limiter
- Slow reaction

This answers the requested **automatic or manual initial shifting when the lights go green**.

It should be an explicit setting:

**Launch Control Mode**

- Automatic
- Assisted
- Manual

Separate this from the normal transmission setting.

---

# 5. Transmission Modes

Transmission handling should be configurable independently of difficulty.

## Full Automatic

Game controls:

- Launch
- Gear changes
- Rev matching
- Clutch

Suitable for casual players.

---

## Automatic Transmission + Manual Launch

Game handles shifts after the player launches.

Useful intermediate mode.

---

## Manual Sequential

Player presses:

- Shift Up
- Shift Down

The clutch is automatically managed.

Shift timing matters.

---

## Manual + Clutch

Player controls:

- Throttle
- Clutch
- Gear
- Launch

This provides maximum performance potential but has the highest skill requirement.

---

# 6. Shift Mechanics

Gear changes should not simply be instantaneous button presses.

Each transmission has:

- Shift speed
- Torque interruption
- Clutch engagement time
- Gear inertia
- Synchro capability
- RPM limits

Example:

Traditional manual gearbox:

**250–600 ms shift**

Performance manual:

**150–350 ms**

Dual-clutch transmission:

**50–150 ms**

Sequential racing gearbox:

**40–120 ms**

These values can be tuned rather than requiring completely realistic simulation.

---

# 7. Rev Matching

Rev matching becomes primarily relevant during downshifts.

For manual transmissions:

Player downshifts.

The game calculates the required engine RPM for the new gear.

Example:

Current:

4th gear\
3,500 RPM

Downshift to 3rd requires:

5,100 RPM

The player must bring the engine toward approximately 5,100 RPM.

Possible ratings:

- Perfect
- Good
- Rough
- Bad
- Money Shift

Effects include:

- Drivetrain stability
- Shift time
- Clutch wear
- Wheel slip
- Engine stress

In Drag Racing V1 this mechanic will appear less frequently than in future circuit modes, but implementing it now establishes the transmission system correctly.

---

# 8. Engine Simulation

Each engine uses an RPM-dependent torque curve.

Instead of a vehicle simply having:

**500 HP**

it has a torque table such as:

|   RPM | Torque |
| ----: | -----: |
| 1,000 | 220 Nm |
| 2,000 | 300 Nm |
| 3,000 | 390 Nm |
| 4,000 | 450 Nm |
| 5,000 | 470 Nm |
| 6,000 | 455 Nm |
| 7,000 | 410 Nm |

The game interpolates values between these points.

Horsepower is derived from torque and RPM rather than existing as an unrelated statistic.

Internally, the game should primarily simulate:

**Torque → Gearbox → Differential → Wheels**

This makes gearing meaningful.

---

# 9. Torque Curves

Different engines should behave differently.

## Naturally Aspirated

Typical characteristics:

- Predictable throttle response
- Smooth torque curve
- Higher RPM power
- No boost delay

## Small Turbo

- Strong midrange
- Fast spool
- Moderate peak power

## Large Turbo

- Weak low RPM
- Turbo lag
- Very strong high RPM output

## Supercharger

- Immediate boost
- Strong low RPM torque
- Parasitic engine load

## Electric

- Immediate torque
- Torque falls at high speed
- No conventional shifting for some cars

Torque curves should visibly change in the garage dyno interface when parts are installed.

---

# 10. Dyno Screen

Every vehicle should have a dyno view showing:

- HP curve
- Torque curve
- Boost curve
- RPM
- Redline
- Shift recommendations

Optional overlays:

**Stock vs Current Build**

Example:

Stock\
247 HP

Current\
386 HP

Peak torque:

Stock\
350 Nm

Current\
512 Nm

This makes upgrades visually understandable.

---

# 11. Real-World Vehicle Data

When possible, cars should contain real-world-inspired values for:

- Engine displacement
- Cylinder count
- Engine layout
- Aspiration
- Factory horsepower
- Factory torque
- Redline
- Weight
- Drive type
- Gear count
- Gear ratios
- Differential ratio
- Tire dimensions
- Drag coefficient
- Wheelbase

If licensed real-world cars aren't available, fictional cars can still be based on plausible engineering values.

Maintain separate fields for:

**Real/reference data**

and

**Gameplay-adjusted data**

This is important because perfect real-world accuracy can damage game balance.

---

# 12. Vehicle Physics Model

The simulation should calculate at least:

Engine torque

→ clutch

→ transmission ratio

→ final drive

→ drivetrain loss

→ wheel torque

→ tire grip

→ acceleration

Then account for:

- Vehicle mass
- Weight transfer
- Tire grip
- Wheel slip
- Aerodynamic drag
- Rolling resistance
- Road gradient if introduced
- Drivetrain loss

This does not need to be a hardcore simulator.

The goal is:

**Mechanically believable behavior with understandable player feedback.**

---

# 13. Tire Slip

Wheel slip must be a major mechanic.

Target slip is not necessarily zero.

A small amount of tire slip can produce better acceleration than perfect static grip.

Display something such as:

**SLIP: 7%**

Possible ranges:

0–3%\
Very little slip

4–10%\
Good launch zone

11–20%\
Moderate wheelspin

20%+\
Heavy wheelspin

Exact optimal values should depend on tire and surface.

Effects:

- Reduced acceleration
- Tire temperature
- Tire wear
- Smoke
- Sound
- Loss of stability

---

# 14. Grip Model

Grip depends on:

- Tire compound
- Tire width
- Tire pressure
- Temperature
- Vehicle weight
- Weight transfer
- Driven wheels
- Suspension
- Surface
- Weather
- Differential
- Torque delivered

Tire types:

- Economy
- Street
- Performance Street
- Semi-Slick
- Drag Radial
- Slick

Higher-performance tires should come with drawbacks such as:

- Cost
- Wear
- Temperature requirements
- Event restrictions

---

# 15. Drivetrain Differences

## FWD

Advantages:

- Cheap
- Lightweight
- Lower drivetrain loss

Disadvantages:

- Reduced launch traction
- Torque steer abstraction
- Front tires overloaded

---

## RWD

Advantages:

- Strong launches
- Good weight transfer
- Large performance ceiling

Disadvantages:

- Easier to spin tires

---

## AWD

Advantages:

- Excellent launches
- High traction

Disadvantages:

- Greater weight
- Higher drivetrain loss
- Expensive upgrades

This gives drivetrain choice meaningful gameplay consequences.

---

# 16. Gear Ratios

Cars have individually simulated gear ratios.

Example:

1st: 3.60\
2nd: 2.19\
3rd: 1.41\
4th: 1.00\
5th: 0.83\
6th: 0.72

Final drive:

4.10

Player tuning can unlock adjustment of:

- Final drive
- Individual ratios
- Number of gears for certain race gearboxes

The tuning screen should estimate:

- Speed at redline
- RPM drop
- Recommended shift RPM

---

# 17. Shift Optimization

The optimal shift point should **not necessarily be redline**.

The simulation compares:

Wheel torque in current gear

against

Wheel torque after shifting.

The ideal shift occurs when the next gear provides greater acceleration.

Therefore one engine might optimally shift at:

6,500 RPM

while another shifts at:

8,200 RPM.

This gives the torque curve practical importance.

---

# 18. Vehicle Rating System

Each car receives individual ratings:

- Power
- Acceleration
- Launch
- Grip
- Weight
- Transmission
- Aero
- Reliability
- Tuning Potential

Overall performance class:

**F → E → D → C → B → A → S**

Optional subdivisions:

F3\
F2\
F1

E3\
E2\
E1

...

S3\
S2\
S1

This gives substantially more granularity.

For example:

**C2 / 487 PP**

where:

C2 = player-friendly class

487 PP = internal performance-point value.

---

# 19. Performance Rating Calculation

Do not calculate rating based only on horsepower.

Performance Points should consider:

- Power curve
- Torque
- Vehicle weight
- Traction
- Tire grip
- Gear ratios
- Drivetrain
- Aerodynamics
- Transmission shift speed
- Launch capability

This prevents a:

900 HP / terrible traction

car from automatically rating higher than a properly built 600 HP car.

---

# 20. Vehicle Stats

The garage should show both simplified and technical data.

## Simple

POWER\
A

GRIP\
B

LAUNCH\
A

SHIFTING\
B

WEIGHT\
C

---

## Detailed

Power\
436 HP

Torque\
558 Nm

Weight\
1,342 kg

Power/Weight\
325 HP/t

0–60 mph\
Estimated 3.8 sec

1/8 mile\
Estimated 7.7 sec

1/4 mile\
Estimated 11.9 sec

Top Speed\
267 km/h

Grip\
1.05 g estimated

These estimates update live while tuning.

---

# 21. Upgrade Categories

## Engine

- Intake
- Exhaust
- Headers
- Camshaft
- Cylinder head
- Pistons
- Connecting rods
- Crankshaft
- ECU
- Fuel system
- Ignition
- Intercooler
- Turbo
- Supercharger
- Engine swap

---

## Transmission

- Clutch
- Flywheel
- Gearbox
- Differential
- Driveshaft
- Axles

---

## Tires

- Compound
- Width
- Diameter

---

## Suspension

- Springs
- Dampers
- Ride height
- Weight transfer setup

---

## Weight Reduction

Stages:

1. Interior reduction
2. Lightweight panels
3. Racing seats
4. Lightweight glass
5. Full race conversion

---

## Aerodynamics

Less important for the 1/4 mile but increasingly important in:

- 1/2 mile
- 1 mile

Upgrades include:

- Front aero
- Rear aero
- Underbody
- Drag reduction

---

# 22. Upgrade Tradeoffs

Upgrades should not always produce purely positive effects.

Example:

Large turbo:

+150 peak HP\
+120 Nm\
-800 RPM spool response\
+heat\
+traction difficulty

Light flywheel:

+faster revving\
+faster shifts\
-harder launches\
-more RPM drop

Aggressive clutch:

+torque capacity\
+fast engagement\
-more difficult manual launches

This produces actual builds rather than linear stat progression.

---

# 23. Reliability

Performance modifications increase stress.

Components have limits.

Example:

Stock transmission capacity:

420 Nm

Engine produces:

570 Nm

Possible effects:

- Accelerated wear
- Slower shifts
- Clutch slip
- Component failure

For casual difficulty modes, failure can be heavily reduced or disabled.

---

# 24. Engine Swaps

Engine swaps should eventually be a major customization feature.

A swap affects:

- Weight
- Weight distribution
- Power
- Torque
- Redline
- Upgrade options
- Sound
- Transmission compatibility

Engine compatibility can use:

Easy\
Moderate\
Extreme

Extreme swaps require expensive conversion parts.

---

# 25. Visual Customization

Players need emotional ownership of their cars.

Options should include:

- Paint
- Metallic paint
- Matte paint
- Pearlescent paint
- Wheels
- Wheel diameter
- Wheel width
- Tire sidewall
- Ride height
- Window tint
- Decals
- Racing stripes
- License plate
- Headlights
- Taillights
- Hood
- Bumpers
- Spoilers
- Exhaust tips

The car remains 2D, but parts can use layered sprites.

---

# 26. 2D Car Rendering Architecture

Build each vehicle using layers:

1. Body
2. Paint mask
3. Windows
4. Wheels
5. Brake components
6. Front bumper
7. Rear bumper
8. Hood
9. Spoiler
10. Lights
11. Decals
12. Effects

This allows a single base car sprite to support many configurations.

---

# 27. Race Camera

Primary view:

**Side-on 2D**

Camera follows the cars horizontally.

Camera effects:

- Slight shake on launch
- Tire smoke
- Exhaust flames
- Gear-change movement
- Turbo blow-off
- Motion blur
- Background parallax

Parallax layers:

- Track
- Barriers
- Crowd
- Buildings
- Mountains/city
- Sky

This provides depth without 3D rendering.

---

# 28. Race HUD

During a race:

### Main HUD

RPM bar\
Current gear\
Speed\
Shift indicator\
Boost\
NOS if installed\
Slip indicator

### Optional advanced HUD

Throttle %

Clutch %

Wheel slip %

Engine RPM

Boost PSI/bar

Engine temperature

Optimal shift marker

Current acceleration G

---

# 29. Shift Feedback

Each manual shift gets feedback.

Examples:

**PERFECT SHIFT**

**GOOD SHIFT**

**EARLY SHIFT**

**LATE SHIFT**

**REV LIMITER**

Perfect shifts should provide only a small advantage.

Manual driving should reward skill without making automatic transmission unusable.

---

# 30. Reaction Time

Reaction time is separate from elapsed time.

Example result:

Reaction\
0.084 sec

60 ft\
1.71 sec

1/8\
7.42 sec

1/4\
11.58 sec

Trap Speed\
196 km/h

This allows players to distinguish:

**Driver performance**

from

**Vehicle performance**

---

# 31. Difficulty System

Difficulty can use five presets.

## Beginner

- Automatic launch
- Automatic transmission
- Optimal shifts
- Strong traction assistance
- Slow AI reaction

## Easy

- Automatic launch
- Optional manual gears
- Shift indicator
- Moderate assists

## Normal

- Assisted launch
- Manual or automatic
- Normal AI
- Moderate traction assistance

## Hard

- Manual launch encouraged
- Reduced assists
- Faster AI reaction
- More optimized opponents

## Simulation

- Manual clutch
- Manual launch
- Wheelspin
- Clutch damage
- Mechanical stress
- Minimal shift assistance
- Strong opponent tuning

Individual assists should still be configurable separately.

---

# 32. Custom Difficulty

Player can independently configure:

Transmission\
Auto / Manual

Launch\
Auto / Assisted / Manual

Clutch\
Auto / Manual

Shift Suggestions\
On / Off

Traction Assist\
High / Medium / Low / Off

Rev Match Assist\
On / Off

Staging\
Auto / Manual

Mechanical Damage\
Off / Reduced / Full

This is preferable to locking everything behind difficulty presets.

---

# 33. AI Opponents

AI vehicles should obey the same physics.

AI characteristics:

- Reaction time
- Launch consistency
- Shift accuracy
- Tuning quality
- Aggressiveness

AI should occasionally make believable mistakes:

- Slight wheelspin
- Early shift
- Late shift
- Poor launch

Higher difficulty primarily improves consistency rather than giving the AI artificial horsepower.

---

# 34. Career Rival System

Career tiers can contain recurring rivals.

Rivals have:

- Name
- Portrait
- Preferred manufacturer
- Signature car
- Driving style
- Difficulty
- Upgrade progression

A rival encountered in D Class might return later with a heavily modified A-Class version of the same car.

This gives the career mode continuity without requiring a complex story.

---

# 35. Economy

Primary currency:

**Cash**

Used for:

- Cars
- Parts
- Repairs
- Tuning
- Customization

Secondary progression:

**Reputation**

Used for unlocking:

- Better dealerships
- Advanced parts
- Special races
- Tuners
- Higher career tiers

Avoid unnecessary premium currencies in the initial version.

---

# 36. Offline / Idle Income

The player's garage generates money while offline.

This should support progression without making racing irrelevant.

Concept:

The player owns a small automotive business.

Possible business types:

- Garage
- Tuning shop
- Parts shop
- Dyno service
- Used-car business

Initially:

**Garage Revenue**

The garage generates cash over time.

Example:

Base income:

$100/hour

Upgrades:

Level 2\
$180/hour

Level 3\
$300/hour

Level 4\
$500/hour

Offline earnings accumulate for a limited period.

Recommended starting cap:

**8 hours**

Upgradeable to:

12 hours\
18 hours\
24 hours

This encourages returning without punishing players for having a life.

---

# 37. Idle Income Interaction

Idle income should not simply be a timer.

Players can assign cars to jobs.

Example:

Customer requests:

**Local promotional race**

Required:

C Class\
RWD

Duration:

2 hours

Reward:

$2,400

Assigning the car makes it temporarily unavailable for racing.

Other jobs:

- Car show
- Test drive
- Promotional event
- Delivery
- Dyno demonstration
- Exhibition race

This integrates the garage collection into the idle system.

---

# 38. Garage Upgrades

Garage progression:

Level 1\
Basic garage

Level 2\
Tuning bay

Level 3\
Dyno

Level 4\
Fabrication shop

Level 5\
Race workshop

Upgrades can unlock:

- More stored cars
- Offline income
- Better tuning
- Engine swaps
- Advanced parts
- More simultaneous idle jobs

---

# 39. Dealership

Cars can be purchased through:

## New Dealer

Predictable vehicles and prices.

## Used Dealer

Rotating inventory.

Used cars may have:

- Mileage
- Modifications
- Wear
- Lower prices
- Rare configurations

## Specialist Dealer

Unlocked later.

Offers:

- Rare cars
- Race cars
- Classic vehicles

---

# 40. Vehicle Condition

Optional but recommended:

- Engine condition
- Transmission condition
- Tire wear

Avoid excessive micromanagement.

Condition should matter primarily when:

- Buying used vehicles
- Running extreme builds
- Participating in long-term career progression

---

# 41. Race Rewards

Rewards depend on:

- Opponent difficulty
- Event tier
- Vehicle disadvantage
- Driving assists

Possible bonuses:

Perfect Launch\
+$100

Perfect Shift ×3\
+$150

No Assist Bonus\
+$250

Underdog Bonus\
+$300

Career rewards should remain the primary progression source.

---

# 42. Performance History

Store a record for every vehicle.

Example:

Best 60 ft\
1.56 sec

Best 1/8\
6.91 sec

Best 1/4\
10.72 sec

Best Trap Speed\
217 km/h

Best Reaction\
0.041 sec

Maximum Dyno Power\
624 HP

Players should be able to compare previous builds.

---

# 43. Tuning Presets

Players can save configurations.

Examples:

**Street**

**1/8 Mile**

**1/4 Mile**

**1/2 Mile**

**High Grip**

**Low Grip**

A preset contains:

- Gearing
- Tire pressure
- Launch RPM
- Suspension
- Boost
- Differential

---

# 44. Telemetry

After races, provide a graph containing:

- Speed
- RPM
- Gear
- Throttle
- Wheel slip

against:

**Time**

Advanced view:

- Acceleration
- Boost
- Engine torque
- Wheel torque

This lets players diagnose why they're slow instead of blindly purchasing upgrades.

---

# 45. Race Analysis

After a race the game should identify obvious problems.

Examples:

**Launch wheelspin cost approximately 0.18 seconds.**

**Your 2→3 shift occurred approximately 500 RPM too late.**

**Third gear is too short for this setup.**

**Your engine drops below its strongest torque range after shifting to fourth.**

These recommendations make realistic systems understandable to less technical players.

---

# 46. Starter Cars

The initial starter choice should demonstrate drivetrain differences.

Example archetypes:

### Lightweight FWD Hatchback

130 HP\
Low weight\
Cheap upgrades

### RWD Coupe

165 HP\
Balanced performance\
Strong tuning potential

### AWD Compact Sedan

150 HP\
Excellent launch\
Heavier

They should initially be F/E class cars.

---

# 47. Initial Vehicle Roster

A realistic first release does not need hundreds of cars.

Recommended:

**20–30 cars**

Distributed approximately:

F: 4\
E: 5\
D: 5\
C: 5\
B: 4\
A: 3\
S: 2

Because modifications allow lower-class cars to reach higher classes, the effective variety is much greater.

---

# 48. Car Archetypes

The initial roster should include:

- Economy hatchback
- Hot hatch
- Compact coupe
- Lightweight roadster
- Muscle car
- Sports sedan
- AWD turbo sedan
- Classic muscle
- Classic Japanese coupe
- Modern sports coupe
- Luxury performance sedan
- Supercar
- Lightweight exotic

This provides meaningful mechanical differences.

---

# 49. Audio

Even with 2D graphics, sound should strongly communicate performance.

Engine audio responds to:

- RPM
- Load
- Throttle
- Turbo
- Transmission
- Exhaust upgrades

Additional effects:

- Turbo spool
- Blow-off valve
- Supercharger whine
- Tire squeal
- Gear change
- Rev limiter
- Launch control
- Backfire

Different engine architectures should eventually use different sound families.

---

# 50. Save Data

Player save should contain:

Player

Garage

Owned vehicles

Vehicle configurations

Installed parts

Inventory

Career progression

Economy

Records

Settings

Idle-income timestamp

Save format should be versioned from the beginning.

Example:

`saveVersion: 1`

This makes future migrations significantly easier.

---

# 51. Offline Earnings Protection

Never blindly trust the player's system clock.

Store:

- Last save timestamp
- Last server timestamp where available
- Maximum offline duration

For a completely offline game, cap rewards to limit clock manipulation.

Example:

Maximum credited offline time:

24 hours

even if the clock jumps forward six months.

---

# 52. Main Screens

Initial game navigation:

## Home

- Current car
- Cash
- Reputation
- Garage income
- Quick Race

## Race

- Career
- Free Race

## Garage

- Cars
- Stats
- Dyno
- Tuning
- Upgrades
- Customization

## Dealership

- New
- Used
- Special

## Workshop

- Parts
- Installation
- Repairs

## Business

- Garage level
- Idle jobs
- Offline income

## Records

- Times
- Cars
- Career statistics

## Settings

- Controls
- Difficulty
- Assists
- Audio
- Graphics

---

# 53. Minimum Race Simulation Variables

Each vehicle should eventually expose something similar to:

### Vehicle

Mass

Wheelbase

Drag coefficient

Frontal area

Drive layout

### Engine

RPM

Idle RPM

Redline

Torque curve

Throttle response

Rotational inertia

Boost

### Transmission

Gear ratios

Final drive

Shift duration

Drivetrain loss

Clutch capacity

### Tires

Radius

Width

Grip coefficient

Temperature

Pressure

Slip ratio

### Environment

Air density

Road grip

Wind

Temperature

These provide enough information for convincing drag physics.

---

# 54. Simulation Frequency

Recommended architecture:

Physics simulation:

**60–120 Hz**

Rendering:

**60 FPS**

Race physics should be deterministic where practical.

This makes:

- Replays
- AI
- Ghost racing
- Future multiplayer

much easier to implement.

---

# 55. Important Architectural Rule

Separate these systems:

**Vehicle Definition**

contains factory data.

**Vehicle Instance**

contains the player's specific car.

**Parts**

modify vehicle parameters.

**Tune**

modifies adjustable parameters.

**Simulation**

calculates the result.

Do not permanently modify the original vehicle values when installing an upgrade.

Instead calculate:

`Factory Car + Installed Parts + Tune = Current Vehicle`

This becomes extremely important as the upgrade system grows.

---

# 56. Suggested Data Structure

Conceptually:

Vehicle

- Identity
- Dimensions
- Mass
- Aero
- Engine
- Transmission
- Differential
- Tires
- Suspension
- Visual configuration

Engine

- Displacement
- Cylinders
- Aspiration
- Redline
- TorqueCurve[]
- BoostCurve[]
- UpgradeSlots[]

Transmission

- Type
- GearRatios[]
- FinalDrive
- ShiftTime
- Efficiency

Tires

- Width
- Radius
- Compound
- GripCurve
- SlipCurve

This should be data-driven rather than hardcoded per vehicle.

---

# 57. Race Calculation Loop

Every physics step:

1. Read throttle.
2. Determine engine RPM.
3. Interpolate torque curve.
4. Apply throttle.
5. Apply boost.
6. Apply engine modifiers.
7. Apply clutch.
8. Apply selected gear.
9. Apply final-drive ratio.
10. Apply drivetrain efficiency.
11. Calculate wheel torque.
12. Calculate available tire grip.
13. Calculate tire slip.
14. Calculate traction force.
15. Calculate aerodynamic drag.
16. Calculate rolling resistance.
17. Calculate acceleration.
18. Update vehicle velocity.
19. Update position.
20. Update engine/drivetrain state.

This creates the foundation for nearly every requested mechanic.

---

# 58. First Playable Version

Do **not** build every system simultaneously.

The first functional milestone should contain:

- 3 cars
- 1 drag strip
- 1/4-mile races
- Torque curves
- Gear simulation
- Tire slip
- Manual shifting
- Automatic shifting
- Manual/automatic launch
- AI opponent
- Race timing
- Basic garage
- Upgrade system
- Save system

Once that physics foundation feels good, add progression.

---

# 59. Second Milestone

Add:

- 10 cars
- Career F–C
- Parts
- Tuning
- Dyno
- Garage customization
- Dealership
- Cash
- Reputation
- Offline income
- Free Race
- Difficulty settings

---

# 60. First Full Release Target

Recommended V1 scope:

**20–30 cars**

**F–S career**

**5–8 drag locations**

**100+ career events**

**1/8, 1/4, 1/2 and 1-mile races**

**Full upgrade system**

**Full tuning**

**Visual customization**

**Garage/idle business**

**Used and new dealerships**

**Vehicle records**

**Telemetry**

**Adjustable assists**

No multiplayer is necessary for V1.

---

# 61. Design Principle

The central rule should be:

> A faster part does not automatically create a faster car.

More horsepower can create:

- More wheelspin
- Worse launches
- Poorer gearing
- More mechanical stress

A properly tuned 450 HP car should sometimes beat a badly configured 600 HP car.

That interaction between **power, traction, gearing and driver skill** should be the core identity of the game.

---

# 62. Long-Term Expansion Architecture

Although not implemented initially, the simulation should be designed so the following can eventually reuse the same vehicle system:

- Roll racing
- Highway racing
- Circuit racing
- Time attack
- Drifting
- Hill climbs
- Street racing
- Multiplayer
- Ghost racing
- Tournaments

The first game should therefore be considered a **vehicle simulation platform whose first gameplay mode is drag racing**, rather than a hardcoded drag-racing-only application.

---

# 63. Fuel Management and Mass — Approved Slice Scope

Fuel is an additional required system. Injection/carburetor sound and fueling behavior do not replace tank quantity or consumption.

**Slice 2 — Driving depth and tooling** includes:

- Per-car tank capacity, current litres, configured fuel density and fuel weight.
- Fixed-tick consumption at idle, neutral revs, staging, loaded driving, shifts, burnout and engine-running shutdown; distinguish fuel cuts from ignition cuts/anti-lag use.
- Fuel mass and tank placement affecting total vehicle mass, centre of gravity, axle loads/grip, acceleration, braking and rolling/driveline load.
- Explicit separation of translating mass, wheel/flywheel rotational inertia and gravitational normal load; replacement-part mass changes are counted once.
- Empty-tank combustion loss with physical coasting, and refuel/restart behavior.
- A quick free **Refuel** button with an **Add litres** input limited to remaining tank space; authoritative click-time clamping prevents overfill.
- Saved player fuel quantity across runs/reloads; test-bench refill/reset/replay remains isolated from player fuel and cash. Pause/offline time does not burn fuel.
- Configurable physical test tools, repeatable fuel/mass/contact scenarios and full chronological telemetry showing fuel L/kg/flow/used alongside forces, slip and energy.

**Slice 3 — Fuel, money, career and idle progression** adds fuel pricing, paid refill transactions and balance of the entire fuel/race/reward/upgrade/garage-income loop. Existing simple economy scaffolding does not constitute that complete milestone. Bench fuel remains free for experiments; player refuelling becomes part of the funded loop, with tested low-fuel/low-cash recovery and interruption-safe saves.

See [GAME_PLAN.md](GAME_PLAN.md) for detailed acceptance criteria and [DRIVING_DEPTH_AUDIT.md](DRIVING_DEPTH_AUDIT.md) for current implementation gaps.

---

# 64. Modular Mechanical Parts, Wear and Inventory — Approved Slice Scope

**Slice 2 must introduce persistent mechanical component wear and tear, replacement and a modular mechanical parts system.** These supersede a compulsory one-way mechanical upgrade ladder and pass-local damage resets. Slice 3 balances the economic consequences rather than introducing wear for the first time.

- Driving behavior changes wear through actual RPM/load/boost, thermal exposure/anti-lag, clutch slip/rough engagement, drivetrain overload, tire slip and brake work as relevant to each component.
- Each owned part has a unique identity, retained condition and appropriate limits/failure behavior. Wear persists through removal, swaps, retries and reloads; unused parts do not accumulate driving wear offline.
- Replace failed/worn parts with compatible new or retained used components. Installing a used part does not repair it. Refuelling/retrying does not repair failed hardware.
- Mechanical components can be mixed, upgraded, downgraded or bought directly at a higher specification without purchasing intermediate ranks. A complete healthy compatible assembly must run with conservative default settings.
- A downgrade may reduce safe boost/RPM/load limits. The game offers safe defaults and explains the affected constraints, rather than silently creating supporting parts or applying a generic tier multiplier.
- Inventory supports buy, install, remove, swap, retain, reuse and sell operations. Removed components return to inventory in their existing condition; sales remove the owned instance and pay its shown value once. Installed/locked parts must be removed before sale.
- Parts can be shared across engine configurations when authored fitment/interface/dimension and dependency rules permit it. Equal advertised specs alone do not prove compatibility. Explain blocked fitment rather than rely solely on rank or engine name.
- OEM engines have explicit component recipes and quality. One engine can ship with forged internals while another compatible engine uses standard parts; OEM does not always mean low quality. Material/quality/strength, fitment and condition are separate properties.
- Example: buy forged pistons directly, keep the original compatible standard set, and reinstall it later when replacing forged parts would be too costly. Apply a suitable conservative tune. Retain the removed forged set for compatible reuse or sale, with its wear unchanged.
- Forged internals do not automatically generate more horsepower; installed-component limits, induction/fueling/tune and authored tradeoffs determine the resulting build.
- Developer tools expose installed parts, compatibility, condition, limiting components and repeatable wear/failure/replacement/downgrade scenarios. Test resets and free test parts never alter or create player inventory.
- Saves and migrations protect instance ownership, cash and installation state against duplication, loss or condition resets. Tuning presets cannot resurrect removed/sold parts.

Slice 2 includes functional replacement/inventory/selling and provisional authorable part prices. Slice 3 balances part acquisition, used-part resale, service/replacement costs and career/idle recovery routes alongside paid fuel. See [modular component requirements](GAME_PLAN.md#required-modular-components-compatibility-and-inventory) and [wear requirements](GAME_PLAN.md#required-persistent-mechanical-wear-and-replacement).

---

# 65. Tire Durability, Race Breakdowns, Repeated Ownership and Salvage

**Slice 2:**

- Tire compounds have independently authored grip, operating temperature, wear rate/rubber reserve and failure limits. Better grip does not imply longer life; high-grip tires may wear faster than lower-grip alternatives.
- Track tire wear from physical driving/slip work, load, pressure and heat, including burnouts and front/rear differences. Condition persists through removal/reuse/reloads and affects appropriate tire behavior. Compound choice is a tradeoff, not a universal durability upgrade rank.
- Racing can break the car mechanically through abuse or accumulated wear. Supported engine/driveline, brake and tire failures change real driving behavior, can cause a DNF and remain after retries/reloads. Preserve coasting/momentum and require the declared replacement/service remedy. Safe default tunes do not make a car indestructible.
- Failures have component-specific consequences: failed brakes remove/reduce braking rather than destroy the engine; failed drive components interrupt their actual power path. AWD must distribute front/rear drive through its declared fixed-bias, open/locked-center, on-demand or active coupling setup. Surviving-axle propulsion requires suitable hardware. An unisolated open differential permits the disconnected shaft to free-spin, receiving most output power while its low resistance limits healthy-axle torque. Show shaft speed/power and branch condition in test tools; preserve road/rotational momentum. Do not invent wall-impact damage until collision behavior exists.

**Slice 3:**

- Buy multiple copies of the same car model. Every owned car/chassis has a unique instance ID and its own hardware, condition, fuel, tune, records and assignments.
- Keep, sell or salvage incomplete/non-running cars and assemblies. Being unable to race does not prevent salvaging existing assets.
- Support keeping an engine assembly while salvaging its chassis. Retained internals/accessories preserve identity/condition; only explicitly disposed assets leave ownership and contribute to the salvage quote.
- Sell/store/transfer complete or partial engines as assemblies, preserving exclusive ownership of their actual component children. Prevent double selling the engine and its constituent parts.
- Pricing accounts for actual equipment/quality, wear/failure, completeness/running state, repair/assembly value and authored model/buyer/market variables. Missing parts have no imaginary value.
- Whole-car and engine assembly prices need not equal the sum of parts. An assembled engine may sell for more or less than the same components loose, depending on the valuation policy and condition/context.
- Ownership/valuation previews and atomic recoverable transactions protect retained assets, cash, selection and assignments during purchase, sale, salvage and interrupted saves.

See [Slice 2 tire/breakdown requirements](GAME_PLAN.md#required-tire-compound-wear-and-race-breakdowns) and [Slice 3 ownership/salvage/valuation requirements](GAME_PLAN.md#required-repeat-ownership-partial-salvage-and-assembly-valuation).

---

# 66. Ordered Implementation Subphases

Implement the approved scope through the dependency-ordered [subphase plan](IMPLEMENTATION_PHASES.md), rather than a single large Slice 2 or Slice 3 change. Slice 2 is **2A–2L** and Slice 3 is **3A–3H**. Each subphase must produce a runnable checkpoint with its defined exit gate, usable controls/tools, relevant persistence and recorded verification before the next begins.

Ownership/save foundations and replacement tools precede persistent fuel/wear consequences. The physical bench/replay foundation precedes the contact rewrite and damage calibration. Valuation precedes paid salvage; a recovery route accompanies paid fuel. The owned vehicle/assembly identity schema is prepared in Slice 2, while repeated model purchases and salvage remain Slice 3 features. Fuel remains free in Slice 2; pricing and complete progression balance remain Slice 3.

The detailed requirements in this objective and `GAME_PLAN.md` are preserved. Completion follows recorded implementation and acceptance gates; see the current status and completion record in IMPLEMENTATION_PHASES.md.

---

# 67. Simulation Difficulty and Accelerated Wear Testing

Allow simulation and arcade driving through shared parameter rules, independently of opponent difficulty and launch/shift modes. Offer Arcade, Balanced and Simulation presets plus individual overrides for heat management, clutch assistance, stall prevention, traction/rev matching and mechanical wear: None, Partial at 25%, or Full. With heat management enabled, race-entry temperatures are Fully cooled (ambient), Optimal (operating targets at start only), or Live (retained heat cooled by actual parked/offline time). With heat management disabled, temperatures remain optimal throughout driving and race-entry temperature choices are disabled. Optional pre-race warm-up permits neutral idle, ordinary driving and physical burnouts before the lights; retain actual heat, tire wear, fuel and fluids when advancing to staged neutral. Partial still permits failures from abuse. None prevents new wear without repairing existing components. Persist these preferences and keep them through game reset.

The physical bench uses those same rules, with an isolated wear multiplier for accelerated tests; exact replay captures both the selected rules and multiplier. Do not accelerate elapsed physics time or friction heat to fake faster wear. Graphs show continuous chronological curves with shift cuts, throttle-off tails and previous overlays. As tire thermal/contact/fuel/cooling systems arrive in Slice 2, integrate their applicable difficulty parameters into this single model.

---

# 68. Readable Car Stats, Part Comparisons and Applied Tuning

Show a full verbose car-stat/dyno view with no comparison when inspecting the saved vehicle, and a comparison overlay when selecting shop/inventory parts or previewing tuning. Show concrete increases/decreases, explanations of favorable changes and tradeoffs, actual inventory wear and component strength separately from power. Warn about likely wheelspin, clutch slip, weak parts, failures and harmful setups with practical suggestions; clearly identify estimates and incomplete future simulation features.

Make numeric stats readable through labelled minimum-to-maximum bars on a shared comparison scale. Display current → proposed values on the right. Highlight the changed segment green for an improvement and red for a drawback; use amber when the change is a contextual tradeoff. Explain that visual ranges are reference scales, not safe operating limits.

Display outer tire radius in inches including rubber, with diameter distinguished from radius/rim size, and boost in bar/psi. Persist shared pressure and speed unit choices. Keep canonical simulation/replay units independent of display choices.

Tuning edits and preset loads require explicit Apply before changing the saved car/race build. Leaving tuning/inventory discards unapplied drafts/selections and returns to saved values. Deliberate shop buy/install and inventory installation transactions may commit hardware immediately, using the saved tune. Failed or stale saves cannot publish an unapplied tune or alter ownership.


## Component effects and purchased tuning equipment

- Every installed option must explain and apply its category-specific physical/acoustic/function effect. Only relevant strength/speed/thermal ratings are shown; non-engine parts cannot impose an arbitrary engine RPM ceiling.
- Cooling hardware controls engine-to-coolant heat transfer, radiator heat rejection and thermal capacity. Better cooling can protect a hotter tune, without repairing damage or granting unrelated power/strength.
- Open, limited-slip, welded and solid-spool differentials must differ in torque allocation and split-grip behavior where applicable. Equal straight-line grip must not create a fake traction bonus; strength/mass and locking tradeoffs are explicit. Full independent angular/contact response still belongs to 2G–2H.
- Optional interior/comfort equipment can be stripped without a running-car prerequisite or mechanical wear. Removed originals remain inventory assets and can be reinstalled/sold. Structural chassis and required engine components remain distinct.
- Rev limiter, two-step controller and shift light are separate purchasable hardware. Fixed/basic versus programmable versions determine available tuning thresholds/behavior/cut method. EFI needs its controller; carbureted cuts respect physical fueling capability. ECU/induction upgrades independently govern compatible preload/anti-lag tuning.
- Parts must include distinct physical turbo sizes and fixed versus adjustable hardware. Size changes response and airflow capacity and requires a part swap; only supported boost/controller, pulley, valve and gearset settings can be tuned. Price or rank does not automatically grant tuning access. The shared resolver must enforce installed-part capabilities and ranges for presets, draft Apply, races and the physical bench; raw sound-engineering experiments remain unrestricted. See [expanded hardware catalog](../reports/part-hardware/CHANGES.md).
- Fuel consumption is a difficulty setting: None (0%), Reduced (25%), Realistic (100%), independently selectable from wear. Arcade/Balanced/Simulation presets select those respective rates. Preserve the fuel preference on save/reload and progression reset, capture it for exact replay and apply it through the same race/physical-bench fuel calculation. Fuel quantity still contributes to weight; pricing remains Slice 3.
- Compatible carbureted engines can install throttle-body or port-injection conversion kits plus injection-capable Controls. Conversion changes fueling, metering/idle/hesitation, fuel flow and the fueling portion of sound while retaining engine/cam/exhaust identity. Retain worn original carburetors for reuse. EFI controller failure stalls supply without destroying internals. Fuel versus ignition cut is a hardware-enabled tuning choice: fixed modules retain calibration, programmable limiter/two-step modules expose compatible choices, and a carburetor cannot claim injector fuel cut. See [fueling conversions](../reports/fueling-conversions/CHANGES.md).
- Shared physical bench exposes installed hardware effects, removal, cooling and split-grip diagnostics without changing pedal ownership. Garage drafts require Apply; leaving discards them. One-time migration preserves prior paid control features and all retained component identities/conditions.

Implementation and scope: [PART_EFFECTS_AND_EQUIPMENT.md](PART_EFFECTS_AND_EQUIPMENT.md).

## Failed-component identification and replacement

- Show selected-car installed failures clearly in Garage, Parts and race results, with failure-first cards, health, cause and explicit FAILED labels. Mark affected shop categories. Surface heavily worn parts separately; the 50% service-display threshold does not change physical failure rules.
- Provide per-component shortcuts to preview and buy/install the same new component, fit a named usable compatible inventory spare with its retained condition, or browse that slot's alternatives/donors. Display the exact purchase price and disable unaffordable actions with the shortfall. Retain removed failed originals in inventory; use the existing atomic ownership/save rules and preserve tuning.
- Refresh counts immediately and after reload. Do not include loose inventory or other vehicles in the selected-car failure count, and do not offer failed inventory spares as quick fixes.
- Physical-bench failures remain isolated, with a visible live failure list and direct selection of the affected component's condition controls. Race-result repair navigation commits final live wear before showing the owned parts.

Implementation: [PART_EFFECTS_AND_EQUIPMENT.md](PART_EFFECTS_AND_EQUIPMENT.md#component-maintenance-shortcuts).

## Basic car-stat summary above details

Keep a compact summary visible above the scrolling generic stats and explanations. Show core power, torque, vehicle weight and standard-probe quarter-mile values for the saved car. In a part/tune preview, show current → proposed values with favorable/unfavorable/tradeoff colors and a short list of relevant changed cooling, coupling, braking, rotating mass, wheel, gearing, induction/equipment or selected-component ratings. Strength changes must remain visible when horsepower is unchanged. Use the selected display units and mark unavailable estimates explicitly. Detailed stats remain available below; summary visibility must not depend on the detail scroll position. Preserve the isolated preview and explicit-Apply rules.


## Approved condition, valvetrain, fluids and service extension

Implement component-specific pre-failure performance/efficiency loss, compatible cam/valvetrain hardware and part-owned timing adjustments, persistent oil/coolant quantities and thermals, meaningful service faults and limited repairs. Top-up restores quantity only; ring service requires a usable bore; fluid mass is accounted once. Add independent None/25%/Full depletion/aging and condition-effect difficulty choices, manual/automatic pre-race top-ups, physical bench controls, exact replay state and practical diagnostics. Preserve existing inventory, draft Apply and pedal ownership behavior. Random faults remain deferred; nitrous receives compatibility metadata only. Operating costs remain Slice 3. See [implementation](WEAR_FLUIDS_AND_SERVICE.md).


Approved addition (2026-10-08): individual engine components/faults, targeted lower-cost replacement/reuse/sale, paid timed shop rebuilds with persisted reservations, and dry/wet/multi-plate/material/centrifugal clutch hardware. See [individual engine and clutch contract](INDIVIDUAL_ENGINE_AND_CLUTCHES.md). Random intermittent faults remain deferred.

The complete change mapping from the initial plan, retained behaviors and revised Slice 2K–2L acceptance gates is [SLICE_2_PLAN_RECONCILIATION.md](SLICE_2_PLAN_RECONCILIATION.md). Implement 2K then 2L against these reconciled requirements before advancing the Slice 3 operating-cost loop.

Integrated 2L tools and calibration contract: [INTEGRATED_TOOLS_CALIBRATION.md](INTEGRATED_TOOLS_CALIBRATION.md).

Slice 2A–2L developer acceptance is complete: see the [2L checkpoint report](../reports/phases/2l/CHANGES.md). Full operating-cost progression starts at planned Slice 3A. Human driving/listening and real-device acceptance remain separate from automated implementation acceptance.

## Approved Slice 3A–3B extension (2026-10-08)

3A expands the authored roster from four to eight and exposes repeat purchases with independent instance selection. New configurations are a three-cylinder hatch, an AWD turbo/DCT sedan, an inline-six GT and a supercharged injected V8. Existing layered graphics are reused until the art pass.

3B adds a named configuration library alongside retained complete/partial engine assemblies: engine hardware with associated controls, whole-car setups and tuning-only snapshots. A saved setup describes desired definitions and applied adjustments, never actual item IDs or condition/resources. It survives selling its parts. Loading previews fitted/loose inventory reuse, missing purchases, total cost and constraints; an explicit atomic Apply can buy missing parts. Old components remain owned, and failed writes cannot charge cash or partially switch hardware. Engine detachment preserves exact descendant identities, faults, fluids and engine adjustments. Keep/dispose previews do not activate priced salvage before 3D. Details: [assemblies and setups](ASSEMBLIES_AND_SETUPS.md).

## Slice 3C valuation contract (2026-10-09)

Value actual owned loose packs and singular elements, retained complete/partial engines, rolling chassis and complete/incomplete cars separately. Use catalogue basis, quality demand, actual shared pack/individual condition, use/service history, completeness and authored buyer/model context. Explain each price change, allow engine assembly offers above or below identical loose-component totals, and exclude missing/kept assets and stored engines from a chassis/car offer. No free value is created by an empty engine identity or a saved configuration blueprint.

Versioned immutable quotes capture exact ownership/condition/resources/tuning and expire; changed revisions, contents, catalogue/policy, reservations and backwards clocks prevent disposal validation. Read-only player controls and editable policy data arrive in 3C. Existing provisional sales remain unchanged. Quote-backed paid operations and salvage arrive in 3D; prices receive full-loop balance in 3G. See [valuation controls and calculation](VALUATION_AND_QUOTES.md).

## Slice 3D paid operations contract (2026-10-09)

Confirmed captured-quote sales cover loose packs/elements, complete/partial engines, chassis and cars. Chassis salvage retains its attached engine tree and original wear/faults/liquids; individual keep controls retain excluded hardware. Cash and ownership commit once in the recoverable candidate/save transaction, with exact disposed/retained IDs in operating receipts. Valid empty garages remain empty through schema-5 migration/reload; no replacement or missing hardware is gifted.

Normal fuel, fluids and applicable service/replacement/rebuild/dealer operations are paid; isolated benches remain free. Auto top-up uses paid service and blocks entry if unaffordable. Bounded real-time recovery work persists offline and funds fuel, repair or an unrestricted replacement without healing or manufacturing assets. Existing setup missing-part Apply and rebuild reservation behavior is preserved. Default dealer pricing prevents immediate stock-disposal profit, while final pricing/recovery balance remains 3G. See [controls and authoring](PAID_OPERATIONS_AND_RECOVERY.md). Next: 3E career/availability.


### Slice 3E implementation (2026-10-09)

The first structured career now has 21 events: the four original IDs, fourteen ordered F–S qualification/rival events and three optional installed-build restriction challenges. Tier names identify competitions; they do not assert each opponent has that exact benchmark PP. Entries capture fees/payout terms, settlement commits records/rewards/resources atomically, and shutdown continues consuming physical resources. Existing mechanical ranks remain directly available; new motorsport compressor offers use independent REP unlocks. See [career progression](CAREER_PROGRESSION.md). Prize cars, 100+ events and broad manufacturer/generation content remain later content, outside this systems phase.


### Slice 3F implementation (2026-10-09)

Four garage business levels preserve existing unrestricted collection storage while expanding income, caps and simultaneous jobs. Prototype display jobs keep engines off, consume no driving resources/wear and reserve exact owned builds until claim or cancellation. Captured rate/deadline/reward contracts survive offline time and policy edits; old-rate/fractional accrual, clock rollback and failed-write recovery are covered. See [garage business](GARAGE_BUSINESS.md). Used/specialist dealer rotations, driving customer jobs and a fifth workshop level remain later content.


### Slice 3G implementation (2026-10-09)

Funded career/loss, real worn-component DNF and paid repair, downgrade/reuse, timed profitable rebuild/assembly sale, idle reinvestment and empty-garage replacement routes have recorded ledgers. Recovery pays $250 per real minute; filter pricing and authorable rebuild labour replace generic costs. Numeric JSON snapshot drift no longer blocks unchanged paid quotes/jobs after driving; currency/IDs/health/failures and meaningful state changes stay strict. See [economy calibration and controls](ECONOMY_BALANCE.md). 3H is still required for integrated Slice-3 acceptance and packaging; this does not deliver the full original long-career/used-dealer/storefront content.


### Slice 3H implementation acceptance (2026-10-09)

3A–3H passed integrated developer acceptance: 13 domain gates, 18 Windows workflows and 16,719 foundation checks, including the 5,662-check / 2,429-run configuration matrix. The approved thermal extension separates optimal at race start from continuous optimal-temperature assistance, with saved live cooling and optional shared warm-up. See [full Slice 3 audit](../reports/SLICE_3_AUDIT.md) and [3H checkpoint](../reports/phases/3h/CHANGES.md). This completes the planned operating-loop systems slice; the larger original content, human/device acceptance and commercial hardening remain later work.


### Tire feedback and stutter audit request (2026-10-09)

Refine tire-slip audio and emit smoke at slipping tire patches, using independent contact in physical modes and declared test inputs in acoustic mode. Preserve shared volume and pause/reset/replay behavior. Audit clutch power loss before changing coupling. Diagnose stutters and report findings before performance changes. The focused implementation/audit is recorded in [tire feedback report](../reports/tire-feedback/CHANGES.md); save, simulation and graph performance fixes are pending review.


## Asynchronous race checkpoints (2026-10-09)

Approved after the tire-feedback stutter report: full recoverable saves remain at race entry, result, pause, exit and other explicit transaction boundaries. During driving, one copied active-car snapshot per simulated second (also on newly detected failures) is queued to a bounded background journal writer. It captures fuel, fluids, component/element condition and the thermal clock without rebuilding the garage ledger. Full saves drain pending writes and retire the journal only after durable publication; interrupted loads validate and merge it before closing the run without payout. The physical bench remains isolated. This increment does not change physics, wear, fixed-tick catch-up, CSV exports or graphs. See [implementation and verification](../reports/async-race-saves/CHANGES.md).


## Tire sound harshness refinement (2026-10-09)

User audition requests recognizable rubber-on-asphalt screech with less white-noise-like grit: replace the four wavering pure-tone voices with prominent noise-excited rubber resonances, remove the broadband hiss bed and reduce low contact texture. Preserve contact-driven intensity, smooth shift chirps and grip recovery, shared volume and pause/reset/export behavior. See [sound refinement and verification](../reports/tire-sound-refinement/CHANGES.md). Physics, smoke and asynchronous race saves remain unchanged.


## Opponent audio, menu responsiveness and shop/inventory extension (2026-10-09)

Approved requested refinements: modulate tire resonance using actual grip/load/radius as well as contact sliding work; opponent-specific audio with distance attenuation and a saved volume under the shared master; concurrent opponent advancement with fixed-tick joins and exact shared-physics parity; immediate dyno plus bounded background copied-car probes; no full saves from tune/part/cart previews, with definitive transaction/Apply and completed settings gestures saved. Parts now provides separate Installed/Inventory/Shop pages, a session-only cart with quantities and cumulative checked-install preview, atomic checkout preserving displaced identities/condition, and current-versus-no-wear graph/stat comparison without healing or refilling actual assets. Existing immediate replacement/service shortcuts remain. See [implementation, timing and 392 named checks](../reports/shop-inventory/CHANGES.md). This completes this spontaneous increment without advancing another planned slice. Full-save/transition pauses and simulation allocations remain documented limits.

## Shared inventory and complete-set purchasing extension (2026-10-09)

Approved: reusable storage/car/engine/seller/cart views over actual records, consistent move/install/buy/sell operations and shared directional pricing; complete component sets by default with actual engine counts, optional individual purchases without choosing fitted positions, and explicit targeted installation/reuse. Preserve identities, faults, wear, liquids, incomplete assemblies, reservations and recoverable saves. Trade carts preview cumulative installations and moves, show charges/credits/net cash, and publish all confirmed operations once. Sold loose parts/units become persisted dealer stock and can be bought back without renewal. Current whole-car/engine salvage valuation remains independent of loose-part sums. See [contract and controls](INVENTORY_COMMERCE.md). This is a post-3H refinement, not a new numbered slice.

# 69. Slice 4 Interface Review and Platform Scope — Approved Plan Revision

Revise interface/input/platform work around all approved Slice 2/3 and post-3H systems, including modular/individual parts, shared storage/trade carts, condition-aware stats and no-wear comparisons, saved configurations, paid service/salvage, thermal/difficulty rules and both development benches. Preserve one authoritative race/physical-bench simulation, exact worn assets/resources, installed tuning permissions, explicit Apply/Revert, keyboard/slider ownership and held throttle through shifts.

The [Slice 4 plan](SLICE_4_PLAN.md) divides work into **4A–4Q** review checkpoints. Start with a complete control map and annotated layouts; migrate individual workflows before controller and real Android validation. Deliver checks, a runnable checkpoint where applicable, interface review instructions and a local commit at each phase. **Wait for explicit user validation before starting the next phase.** Technical passing results and silence are not user acceptance. Layout/behavior fixes remain part of the current checkpoint until reviewed.

Functional interface clarity, visible hardware-aware shift light, readable continuous graphs, remapping, scalable navigation and lifecycle/save/input parity are Slice 4. Final vehicle art/content is Slice 5; commercial distribution/hardening is Slice 6. This plan revision does not implement 4A, change gameplay calibration or authorize a bulk UI replacement.

4A review delivery (2026-10-09): [annotated clickable layouts, control/workflow coverage and proposed framework](ui/4a/README.md). Existing advanced controls, real ownership, Apply/Revert and race/bench distinctions are mapped. This is ready for user validation, not accepted runtime UI; no packages, game data or saves changed. 4B waits for explicit approval.

Slice 4 direction updated (2026-10-09): the user declines the web interface proposal and will specify native layouts while reviewing one button or slider at a time. A lettered phase is an organizer, not authority to migrate a whole screen. Implement **F8 whole-main-interface Old ↔ New comparison now**, even with every new interface empty; preserve underlying panels, state, held driving inputs and simulation. Keep the existing UI recoverable through F8. The sound-bench scenario-B keyboard command moves to Shift+F8 to avoid the existing collision. This requested foundation is 4B.1 only; no other control or framework/package work is authorized by it.

Native visual/navigation requirements recorded (2026-10-09): [grey/red style, top navigation, flexible dialogs and Settings-first contract](NATIVE_UI_DESIGN.md). Primary/confirmation buttons are red; secondary actions grey; most buttons have bordered small rounded corners, shading and hover/focus effects beyond pixel-sprite constraints. Most menus are opaque full-screen views hiding the strip, with consistent top navigation showing cash, selected-car fuel and progression; Settings is at the far top right. Race/other appropriate contexts may use Back or pause instead. Full-screen tabbed Settings incorporates Difficulty/driving, Sound and the other preference categories, removing the separate Driving navigation destination in the new UI. Dialogs sit above the interface, include a top-right X/cancel action, and support forms/sliders/previews. All new Settings edits are draft only until explicit Save and confirmation; this supersedes gesture-end auto-save/live application for that migration. Record these requirements now; implement Settings first afterward, one reviewed control at a time.

Native UI refinement (2026-10-09): prefer recognizable icons over text wherever practical. Small info buttons open dialogs for secondary details, including deep part descriptions/specifications; primary part summaries show HP, torque, weight and durability plus actual change effects, with essential warnings still visible. Layouts must adapt to resolution, aspect ratio and fullscreen/windowed changes with consistent shared scaling and anchors wherever possible: navigation is anchored top/full-width, and Settings is right-anchored with its hit area occupying the bar's full height. Preserve icon proportions, readable controls and draft/state across resizing. See the [icon/detail and responsive-layout contract](NATIVE_UI_DESIGN.md). Requirements recorded only; no new control is implemented by this refinement.

Native UI choices (2026-10-09): use filled icons; support 1080p properly with sharp 4K scaling, retaining current windowed mode/default dimensions and avoiding unnecessary scaling of most elements. Main navigation order is Garage, Build, Tuning, Race, Business, Data. Reserve a current-car inspection button immediately left of Settings for a future right-side slide-in overlay containing car preview, dyno, full specifications/statistics, warnings, wear and broken parts. Do not implement that drawer yet or add benches to the new player navigation; retain existing development tools. [Design Racing Game Navigation](https://chatgpt.com/c/6ac9abdf-4ff0-83e9-a4e4-7552526c48bc) is a content reference only, not approval of every suggestion. After initial access limits, both design replies were read from the user-opened sidebar; [reference notes](NATIVE_UI_REFERENCE_NOTES.md) distinguish possible panel content from unselected layouts/additional mechanics. See [native contract](NATIVE_UI_DESIGN.md). Documentation only; Settings remains the first implementation target.

First native implementation requested/delivered (2026-10-09): [4B.2/4C.0 main navigation and Settings shell](../reports/phases/4b/05-NATIVE-SHELL.md), using built-in Unity UI Toolkit, filled vector icons, grey/red rounded/shaded states and opaque full-window menus. Six destinations, real top-bar resources and right/full-height Settings gear work; Settings category tabs retain selection across F8/resizing. Other page content remains placeholder with old controls reachable through F8. Preference sliders/inputs, explicit Save/confirmation, inspection drawer and further migrations are not implemented yet. Updated Windows build, 89 native shell checks and 48 comparison regressions pass; true 4K render target captured, actual window width limited by monitor. Stop for user validation before the next Settings control; this does not complete 4B or 4C.


Native shell style revision (2026-10-09): use the user's screenshot as the navigation/button reference with a modern cel-shaded racing theme, bundled condensed racing typography, shaded multitone buttons and a brief hover/focus shine. Active navigation has a persistent circular red glow from its bottom center with a brighter core, fading upward/outward. The user rejected harsh flat shading bands: use subtle smooth faces and fine bevels. Background stays a static solid color. UI copy must be concise: no unnecessary titles, subtitles, descriptions, repeated tab headings or development placeholders. Preserve meaningful values/units/warnings and secondary information behind info actions. This revises only the current shell, not Settings controls or later phases. See [style checkpoint](../reports/phases/4b/06-RACING-STYLE.md); stop for another visual review.


Native visual correction (2026-10-10): the user's two new references request a highlighted parallelogram Settings face, partial red bottom trim at both ends of the header (not a full-width red border), segmented footer red trim with angular terminal details, and a brushed-metal background with subtle grain/noise on surfaces/buttons. This supersedes the earlier flat-color background direction while keeping it static. Keep existing navigation selection glow, fonts, concise copy, full-height Settings click area, responsive anchors and F8 state preservation. No extra preference control or later phase is authorized by this visual correction. See [metal/trim revision](../reports/phases/4b/07-METAL-TRIM.md); stop for visual review.

Native Settings request (2026-10-10): explicitly implement all existing menu Settings/Driving options together, using the supplied Settings screenshot for control styling while preserving actual option meanings. Keep the styled header layout; remove only the requested terminal bars/glint, fade red trim to transparency, mirror the bottom-right decoration, smooth gradients with stable dithering and make hover shine complete once without click/state restart. Use brushed slanted tabs with a straight first left edge, matching choices, switches, sliders/numeric entry and upper-left/lower-right red panel corners. All five categories share draft-only edits until Apply/confirmation. Page Cancel discards and returns to the previous selected menu; Revert stays and reloads applied values. Preserve thermal dependencies, reduced 25% rates, existing hardware/physics behavior and separate progression-reset confirmation. This expressly expands this checkpoint beyond the earlier one-control scope; stop for visual validation afterward. See [delivery and checks](../reports/phases/4b/08-SETTINGS.md).

Native Settings visual refinement (2026-10-10): shorten rows/actions; reduce action-button slant; antialias angled/rounded edges; clip every face, texture and hover sweep to its actual outline. Joined choices round only the first/last outer ends. Use matching numeric typography, centered slider thumbs, animated glossy switches with smooth reversal, a narrow themed scrollbar and visible rounded upper-left/lower-right red panel accents. Add restrained brushed-metal content texture. Keep the existing header layout, concise copy, all settings meanings, draft Apply/Cancel and F8. This correction stays within the current Settings checkpoint; stop for visual review. See [control refinement](../reports/phases/4b/09-CONTROL-POLISH.md).

Native Settings palette/layout correction (2026-10-10): align Reset game to the right of its General data row and use a subdued charcoal action with an accent outline; retain its independent destructive confirmation. Keep panel corner highlights short with a local 80-point fade rather than interpolating red along a full edge. Align the scrollbar inside the rounded frame with symmetric vertical insets. Header, content, switches, sliders and dialogs share the native accent from `Game/Assets/DragLab/Resources/UI/NativePalette.uss`; brightness/white/alpha variations are derived from that token. Core background, text and face tokens live there too. This is palette infrastructure and correction within the current Settings checkpoint; no extra player preference or later interface is authorized. See [palette/layout correction](../reports/phases/4b/10-PALETTE-LAYOUT.md).

Native control decision (2026-10-10): remove active-element/focus highlights and Tab/Shift+Tab menu navigation. The user explicitly retains mouse-down styling; also preserve existing hover shine, selected page/value states and mouse-focused numeric typing. Consolidate/reuse the current palette, buttons, tabs, sliders, inputs, switches, panels, scrolling and dialogs according to [shared native control standard](NATIVE_UI_DESIGN.md#shared-native-control-standard-2026-10-10). No later interface is started.

Native Settings correction (2026-10-10): put bulk Driving/Simulation presets in a visibly separate group above individual settings, keeping their existing scopes and deferred Apply behavior. Inset the top-right Settings button slightly (12 logical points), center control text vertically and give dialogs the same top-left/bottom-right fading corner highlights as the settings frame. Keep shared controls and no-focus/no-Tab policy with mouse-down styling. See [hierarchy/alignment checkpoint](../reports/phases/4b/12-HIERARCHY-ALIGNMENT.md).

Dialog X must have visible mouse-hover feedback using the shared native theme, retain mouse-down styling and clear hover on pointer leave without showing a focus highlight. [Close hover checkpoint](../reports/phases/4b/13-CLOSE-HOVER.md) (2026-10-10).

Native browser direction (2026-10-10): cars/parts use grids with a searchable/filterable field and sorting, reusing the shared native theme. Subpages/grouping should simplify navigation; exact grouping is reviewed one control at a time. Garage is the recommended next screen, not yet authorized for implementation by the planning question. Bottom footer ornaments must be straight diagonal lines mirrored at both ends, instead of bent/squiggly terminals. The innermost diagonal must connect cleanly to the horizontal red line. See [browser direction and proposed checkpoints](NATIVE_UI_DESIGN.md#car-and-part-browsers-2026-10-10).

Native icons must stay vertically centered beside the already centered button/resource text. Align visible silhouettes and text as one shared group; keep the existing text centering and button interactions. [Icon alignment checkpoint](../reports/phases/4b/16-ICON-ALIGNMENT.md) (2026-10-10).
