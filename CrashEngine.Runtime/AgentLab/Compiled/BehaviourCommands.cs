using CrashEngine.Importer.AgentLabReadable;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab.Compiled;

// Amedo 2026-11-18
public enum AssignTarget : uint
{
    Focus = 0x0,
    Slot = 0x1,
    SecondSlot = 0x2,
    StoredPosition = 0x3,
}

// Amedo 2026-11-18
public enum AttachSource : uint
{
    Focus = 0x0,
    Slot = 0x1,
    Cached = 0x2,
}

// Amedo 2026-11-18
public enum ConstraintType : uint
{
    None = 0x0,
    Point = 0x1,
    Line = 0x2,
    Plane = 0x3,
}

// Amedo 2026-11-18
public enum Designator : uint
{
    Spawner = 0xDF,
    RouteEdgeEnd = 0xEF,
    Self = 0xF0,
    Player = 0xF2,
    BlastSender = 0xF5,
    StoredPosition = 0xF6,
    SecondSlot = 0xF7,
    Slot = 0xF8,
    RouteNode = 0xFA,
    FocusObject = 0xFB,
    FocusPosition = 0xFC,
    NextKey = 0xFD,
    CurrentKey = 0xFE,
    None = 0xFF,
}

// Amedo 2026-11-18
public enum MessageMode : uint
{
    Single = 0xE0,
    Linked = 0xE1,
    AllLinked = 0xE3,
    LinkedLast = 0xF1,
}

// Amedo 2026-11-18
public enum SensorType : uint
{
    Flee = 0x0,
    Decay = 0x1,
    Speed = 0x2,
}

// Amedo 2026-11-18
public enum Toggle : uint
{
    Keep = 0x0,
    On = 0x1,
    Off = 0x2,
    Same = 0x3,
}

// Amedo 2026-11-18
public enum WobbleWave : uint
{
    None = 0x0,
    Sine = 0x1,
    Square = 0x2,
    SquareAlt = 0x3,
    Random = 0x4,
    Spin = 0x5,
}

// Amedo 2026-11-18
public static class BehaviourCommands
{
    public static PS2BehaviourCommand AddTrail(uint? type = null, uint? matrix = null, bool? rotate = null, uint? axis = null, bool? rateBySpeed = null, bool? shake = null, bool? animTimed = null, bool? useVolume = null, bool? usePitch = null, bool? randomPitch = null, bool? useOffset = null, uint? selector = null, uint? soundCount = null, uint? category = null, bool? follow = null, uint? filter = null, uint? system = null, uint? sound0 = null, uint? sound1 = null, uint? sound2 = null, uint? sound3 = null, uint? sound4 = null, uint? sound5 = null, uint? sound6 = null, uint? sound7 = null, uint? sound8 = null, uint? spawn = null, uint? attach = null, float? speedThreshold = null, float? interval = null, float? jitter = null, float? shakeXZ = null, float? shakeY = null, float? shakeStrength = null, float? shakeFalloff = null, float? volume = null, float? pitchSpread = null, float? pitch = null, float? animTime = null, float? type8Threshold = null, float? rateScale = null, float? offsetX = null, float? offsetY = null, float? offsetZ = null, float? offsetW = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null, uint? pad21 = null, uint? pad22 = null, uint? pad23 = null, uint? pad24 = null, uint? pad25 = null, uint? pad26 = null, uint? pad27 = null, uint? pad28 = null) =>
        Build(CommandId.AddTrail, [type, matrix, Flag(rotate, false), axis, Flag(rateBySpeed, false), Flag(shake, false), Flag(animTimed, false), Flag(useVolume, false), Flag(usePitch, false), Flag(randomPitch, false), Flag(useOffset, false), selector, soundCount, category, Flag(follow, false), filter, system, sound0, sound1, sound2, sound3, sound4, sound5, sound6, sound7, sound8, spawn, attach, Bits(speedThreshold), Bits(interval), Bits(jitter), Bits(shakeXZ), Bits(shakeY), Bits(shakeStrength), Bits(shakeFalloff), Bits(volume), Bits(pitchSpread), Bits(pitch), Bits(animTime), Bits(type8Threshold), Bits(rateScale), Bits(offsetX), Bits(offsetY), Bits(offsetZ), Bits(offsetW)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24, pad25, pad26, pad27, pad28]);

    public static PS2BehaviourCommand ClearTrail() => Cmd0(CommandId.ClearTrail);

    public static PS2BehaviourCommand PositionWarp(Designator key, uint? participant = null, uint? space = null, bool? useOffset = null, bool? face = null, bool? body = null, bool? water = null, float? x = null, float? y = null, float? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.PositionWarp, [participant, (uint?)key, space, Flag(useOffset, false), Flag(face, false), Flag(body, false), Flag(water, false), Bits(x), Bits(y), Bits(z)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand SetKey(uint? key = null, uint? first = null, uint? last = null, bool? notCurrent = null, bool? nearest = null, bool? random = null, bool? toLast = null, uint? pad0 = null) =>
        Build(CommandId.SetKey, [key, first, last, Flag(notCurrent, false), Flag(nearest, false), Flag(random, false), Flag(toLast, false)], [pad0]);

    public static PS2BehaviourCommand NextKey() => Cmd0(CommandId.NextKey);

    public static PS2BehaviourCommand SpawnResidentAgent(float? x = null, float? y = null, float? z = null, ScriptValue? subtype = null, uint? objectId = null, Designator? key = null, uint? activityRadius = null, uint? message = null, uint? joint = null, uint? space = null, bool? flag29 = null, bool? useOffset = null, bool? attach = null, bool? link = null, bool? atFocusPosition = null, bool? atFocusObject = null, bool? setRotation = null, bool? copySelf = null, bool? focusOnSpawned = null, bool? toSecondSlot = null, bool? copyStateSlot = null, bool? copyCached = null, bool? linkBack = null, bool? passSlot = null, bool? passCached = null, bool? passFocus = null, bool? passStored = null, bool? linkAll = null, bool? setSubtype = null, bool? atStored = null, bool? busy = null, bool? notBusy = null, bool? inheritKeys = null, bool? inheritPaths = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.SpawnResidentAgent, [Bits(x), Bits(y), Bits(z), subtype?.Word, objectId, (uint?)key, activityRadius, message, joint, space, Flag(flag29, false), Flag(useOffset, false), Flag(attach, false), Flag(link, false), Flag(atFocusPosition, false), Flag(atFocusObject, false), Flag(setRotation, false), Flag(copySelf, false), Flag(focusOnSpawned, false), Flag(toSecondSlot, false), Flag(copyStateSlot, false), Flag(copyCached, false), Flag(linkBack, false), Flag(passSlot, false), Flag(passCached, false), Flag(passFocus, false), Flag(passStored, false), Flag(linkAll, false), Flag(setSubtype, false), Flag(atStored, false), Flag(busy, false), Flag(notBusy, false), Flag(inheritKeys, false), Flag(inheritPaths, false)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand DoAnimation(ScriptValue blend, byte[]? slots = null, uint? flags = null, ScriptValue? p2 = null, ScriptValue? p3 = null, float? p4 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.DoAnimation, [slots, blend.Word, flags, p2?.Word, p3?.Word, Bits(p4)], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand DoParticle(uint system, uint? count = null, bool? attached = null, bool? attached2 = null, uint? frame = null, bool? local = null, uint? axis = null, bool? useOffset = null, Designator? target = null, uint? actorOverride = null, uint? key = null, float? x = null, float? y = null, float? z = null, float? w = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.DoParticle, [system, count, Flag(attached, false), Flag(attached2, false), frame, Flag(local, false), axis, Flag(useOffset, false), (uint?)target, actorOverride, key, Bits(x), Bits(y), Bits(z), Bits(w)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand DoSound(uint sound0, uint? count = null, uint? category = null, bool? trackedNoStore = null, bool? tracked = null, bool? randomPitch = null, bool? randomVolume = null, bool? setPitch = null, bool? variableVolume = null, bool? shake = null, uint? surface = null, bool? store = null, uint? sound1 = null, uint? sound2 = null, uint? sound3 = null, uint? sound4 = null, uint? sound5 = null, uint? sound6 = null, uint? sound7 = null, ScriptValue? volume = null, float? pitch = null, float? pitchSpread = null, float? volumeSpread = null, float? shakeStrength = null, float? shakeXZ = null, float? shakeY = null, float? shakeFalloff = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null) =>
        Build(CommandId.DoSound, [count, category, Flag(trackedNoStore, false), Flag(tracked, false), Flag(randomPitch, false), Flag(randomVolume, false), Flag(setPitch, false), Flag(variableVolume, false), Flag(shake, false), surface, Flag(store, false), sound0, sound1, sound2, sound3, sound4, sound5, sound6, sound7, volume?.Word, Bits(pitch), Bits(pitchSpread), Bits(volumeSpread), Bits(shakeStrength), Bits(shakeXZ), Bits(shakeY), Bits(shakeFalloff)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14]);

    public static PS2BehaviourCommand SetWobble(float? freqX = null, float? freqY = null, float? freqZ = null, float? ampX = null, float? ampY = null, float? ampZ = null, float? phaseX = null, float? phaseY = null, float? phaseZ = null, float? stiffness = null, float? rate = null, float? duration = null, uint? rest = null, uint? space = null, WobbleWave? waveX = null, WobbleWave? waveY = null, WobbleWave? waveZ = null, bool? rotate = null, bool? fade = null, bool? randomPhaseX = null, bool? randomPhaseY = null, bool? randomPhaseZ = null, uint? signX = null, uint? signY = null, uint? signZ = null, uint? follow = null, bool? axisX = null, bool? axisY = null, bool? axisZ = null, bool? easeIn = null, float? contactGrip = null, float? ownerGrip = null, bool? snapToRest = null, uint? mode = null, bool? useCenterOfMass = null, bool? faceVelocity = null, bool? stickOnTouch = null, bool? wobbleOnTouch = null, bool? preWobble = null, bool? messageOnTouch = null, uint? components = null, float? push = null, uint? touchMessage = null, uint? stickMessage = null, uint? stickFilter = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null, uint? pad21 = null, uint? pad22 = null, uint? pad23 = null, uint? pad24 = null, uint? pad25 = null, uint? pad26 = null, uint? pad27 = null, uint? pad28 = null, uint? pad29 = null, uint? pad30 = null, uint? pad31 = null, uint? pad32 = null, uint? pad33 = null) =>
        Build(CommandId.SetWobble, [Bits(freqX), Bits(freqY), Bits(freqZ), Bits(ampX), Bits(ampY), Bits(ampZ), Bits(phaseX), Bits(phaseY), Bits(phaseZ), Bits(stiffness), Bits(rate), Bits(duration), rest, space, (uint?)waveX, (uint?)waveY, (uint?)waveZ, Flag(rotate, false), Flag(fade, false), Flag(randomPhaseX, false), Flag(randomPhaseY, false), Flag(randomPhaseZ, false), signX, signY, signZ, follow, Flag(axisX, false), Flag(axisY, false), Flag(axisZ, false), Flag(easeIn, false), Bits(contactGrip), Bits(ownerGrip), Flag(snapToRest, false), mode, Flag(useCenterOfMass, false), Flag(faceVelocity, false), Flag(stickOnTouch, false), Flag(wobbleOnTouch, false), Flag(preWobble, false), Flag(messageOnTouch, false), components, Bits(push), touchMessage, stickMessage, stickFilter], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24, pad25, pad26, pad27, pad28, pad29, pad30, pad31, pad32, pad33]);

    public static PS2BehaviourCommand ClearWobble() => Cmd0(CommandId.ClearWobble);

    public static PS2BehaviourCommand NowMoveForwards(ScriptValue speed, uint? pad0 = null) =>
        Build(CommandId.NowMoveForwards, [speed.Word], [pad0]);

    public static PS2BehaviourCommand NowRotateJoint(uint mode, uint? pad0 = null) =>
        Build(CommandId.NowRotateJoint, [mode], [pad0]);

    public static PS2BehaviourCommand StoreCurrentSpace(uint? participant = null, Designator? key = null, uint? pad0 = null) =>
        Build(CommandId.StoreCurrentSpace, [participant, (uint?)key], [pad0]);

    public static PS2BehaviourCommand SetFocusToKey(uint key, AssignTarget? into = null, bool? ofFocus = null, bool? ofSlot = null, uint? pad0 = null) =>
        Build(CommandId.SetFocusToKey, [key, (uint?)into, Flag(ofFocus, false), Flag(ofSlot, false)], [pad0]);

    public static PS2BehaviourCommand RotationWarp(uint? space = null, bool? relative = null, bool? face = null, float? qx = null, float? qy = null, float? qz = null, float? qw = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.RotationWarp, [space, Flag(relative, false), Flag(face, false), Bits(qx), Bits(qy), Bits(qz), Bits(qw)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand ClearThreats(bool? restartable = null, uint? pad0 = null) =>
        Build(CommandId.ClearThreats, [Flag(restartable, false)], [pad0]);

    public static PS2BehaviourCommand TriggerLinkedObjects(bool? fromRequester = null, uint? pad0 = null) =>
        Build(CommandId.TriggerLinkedObjects, [Flag(fromRequester, false)], [pad0]);

    public static PS2BehaviourCommand SetState(uint? on = null, uint? pad0 = null) =>
        Build(CommandId.SetState, [on], [pad0]);

    public static PS2BehaviourCommand ToggleState() => Cmd0(CommandId.ToggleState);

    public static PS2BehaviourCommand NextRouteNode() => Cmd0(CommandId.NextRouteNode);

    public static PS2BehaviourCommand DiscardRoute() => Cmd0(CommandId.DiscardRoute);

    public static PS2BehaviourCommand SetLogicalRadius(float radius, bool? fromBox = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetLogicalRadius, [Flag(fromBox, false), Bits(radius)], [pad0, pad1]);

    public static PS2BehaviourCommand SetBehaviourPriority(uint priority, uint? pad0 = null) =>
        Build(CommandId.SetBehaviourPriority, [priority], [pad0]);

    public static PS2BehaviourCommand SetCollisions(float? centerX = null, float? centerY = null, float? centerZ = null, uint? pairType = null, uint? terrainType = null, bool? setPairType = null, bool? setTerrainType = null, bool? setRadius = null, bool? setGravity = null, bool? pushesOthers = null, bool? setDamping = null, bool? setGrip = null, bool? setBounce = null, bool? setMass = null, uint? bodyBits = null, bool? setCenterOfMass = null, bool? airborne = null, bool? setMaxTilt = null, bool? setTiltPower = null, bool? setTwist = null, bool? setContactDecel = null, bool? state4 = null, bool? state8 = null, bool? radiusFromBox = null, bool? radiusFromHeight = null, bool? clampImpulse = null, bool? fixedImpulse = null, bool? state800000 = null, float? radius = null, float? damping = null, float? contactDecel = null, float? grip = null, float? bounce = null, float? mass = null, float? twist = null, float? ratio = null, float? impulseLimit = null, ScriptValue? gravity = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null) =>
        Build(CommandId.SetCollisions, [Bits(centerX), Bits(centerY), Bits(centerZ), pairType, terrainType, Flag(setPairType, false), Flag(setTerrainType, false), Flag(setRadius, false), Flag(setGravity, false), Flag(pushesOthers, false), Flag(setDamping, false), Flag(setGrip, false), Flag(setBounce, false), Flag(setMass, false), bodyBits, Flag(setCenterOfMass, false), Flag(airborne, false), Flag(setMaxTilt, false), Flag(setTiltPower, false), Flag(setTwist, false), Flag(setContactDecel, false), Flag(state4, false), Flag(state8, false), Flag(radiusFromBox, false), Flag(radiusFromHeight, false), Flag(clampImpulse, false), Flag(fixedImpulse, false), Flag(state800000, false), Bits(radius), Bits(damping), Bits(contactDecel), Bits(grip), Bits(bounce), Bits(mass), Bits(twist), Bits(ratio), Bits(impulseLimit), gravity?.Word], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16]);

    public static PS2BehaviourCommand SetFocusToAgent(Designator agent, AssignTarget? into = null, uint? pad0 = null) =>
        Build(CommandId.SetFocusToAgent, [(uint?)agent, (uint?)into], [pad0]);

    public static PS2BehaviourCommand AttachFocusObject(AttachSource? source = null, uint? exit = null, uint? mode = null, bool? hasOffset = null, bool? hasRotation = null, uint? link = null, bool? busy = null, bool? reverse = null, float? x = null, float? y = null, float? z = null, int? rx = null, int? ry = null, int? rz = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.AttachFocusObject, [(uint?)source, exit, mode, Flag(hasOffset, false), Flag(hasRotation, false), link, Flag(busy, false), Flag(reverse, false), Bits(x), Bits(y), Bits(z), Int(rx), Int(ry), Int(rz)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand DropAttachedObject(uint? message = null, uint? exit = null, uint? mode = null, bool? notBusy = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.DropAttachedObject, [message, exit, mode, Flag(notBusy, false)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand ThrowAttachedObject(uint exit, float x, float z, ScriptValue gravity, ScriptValue speed, uint? space = null, uint? participant = null, Designator? key = null, bool? useOffset = null, bool? jitter = null, bool? clearBusy = null, float? y = null, float? w = null, float? jitterRadius = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.ThrowAttachedObject, [exit, space, participant, (uint?)key, Flag(useOffset, false), Flag(jitter, false), Flag(clearBusy, false), Bits(x), Bits(y), Bits(z), Bits(w), gravity.Word, speed.Word, Bits(jitterRadius)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand ClearFocus() => Cmd0(CommandId.ClearFocus);

    public static PS2BehaviourCommand ClearCollisions() => Cmd0(CommandId.ClearCollisions);

    public static PS2BehaviourCommand SendUserMessage(uint message, uint? to = null, MessageMode? mode = null, uint? selector = null, uint? p2 = null, uint? p3 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null) =>
        Build(CommandId.SendUserMessage, [message, to, (uint?)mode, selector, p2, p3], [pad0, pad1, pad2, pad3]);

    public static PS2BehaviourCommand BroadcastUserMessage(uint message, float radius, bool? useOffset = null, uint? space = null, uint? participant = null, Designator? key = null, float? x = null, float? y = null, float? z = null, uint? filter = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null) =>
        Build(CommandId.BroadcastUserMessage, [message, Flag(useOffset, false), space, participant, (uint?)key, Bits(radius), Bits(x), Bits(y), Bits(z), filter], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12]);

    public static PS2BehaviourCommand ClearAnimation(uint joint, ScriptValue? time = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.ClearAnimation, [joint, time?.Word], [pad0, pad1]);

    public static PS2BehaviourCommand RequestAttachmentFocus(uint? exit = null, uint? pad0 = null) =>
        Build(CommandId.RequestAttachmentFocus, [exit], [pad0]);

    public static PS2BehaviourCommand RequestMessengersFocus(AssignTarget? storeIn = null, uint? pad0 = null) =>
        Build(CommandId.RequestMessengersFocus, [(uint?)storeIn], [pad0]);

    public static PS2BehaviourCommand SetFocusPosition(Designator key, float? x = null, float? y = null, float? z = null, uint? participant = null, uint? space = null, bool? useOffset = null, bool? jitter = null, AssignTarget? into = null, bool? projectX = null, bool? projectY = null, bool? projectZ = null, bool? initialFrame = null, bool? relative = null, bool? focusJoint = null, bool? forward = null, float? jitterScale = null, float? jitterY = null, float? routeBisect = null, float? routeRandom = null, float? sensorDistance = null, float? awayFromPlayer = null, float? routeSide = null, float? routeUp = null, float? routeToward = null, uint? joint = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null) =>
        Build(CommandId.SetFocusPosition, [Bits(x), Bits(y), Bits(z), participant, (uint?)key, space, Flag(useOffset, false), Flag(jitter, false), (uint?)into, Flag(projectX, false), Flag(projectY, false), Flag(projectZ, false), Flag(initialFrame, false), Flag(relative, false), Flag(focusJoint, false), Flag(forward, false), Bits(jitterScale), Bits(jitterY), Bits(routeBisect), Bits(routeRandom), Bits(sensorDistance), Bits(awayFromPlayer), Bits(routeSide), Bits(routeUp), Bits(routeToward), joint], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20]);

    public static PS2BehaviourCommand StopMoving() => Cmd0(CommandId.StopMoving);

    public static PS2BehaviourCommand AddNoiseToFocusPosition(ScriptValue radius, AssignTarget? into = null, float? x = null, float? y = null, float? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.AddNoiseToFocusPosition, [(uint?)into, radius.Word, Bits(x), Bits(y), Bits(z)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand RestartDefaultBehaviour() =>
        Build(CommandId.RestartDefaultBehaviour, [], []);

    public static PS2BehaviourCommand AttachLinkedObject(AttachSource? source = null, uint? exit = null, uint? mode = null, bool? hasOffset = null, bool? hasRotation = null, uint? link = null, bool? busy = null, bool? reverse = null, float? x = null, float? y = null, float? z = null, int? rx = null, int? ry = null, int? rz = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.AttachLinkedObject, [(uint?)source, exit, mode, Flag(hasOffset, false), Flag(hasRotation, false), link, Flag(busy, false), Flag(reverse, false), Bits(x), Bits(y), Bits(z), Int(rx), Int(ry), Int(rz)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand MessageLinkedObject(uint message, uint? to = null, MessageMode? mode = null, uint? selector = null, uint? p2 = null, uint? p3 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null) =>
        Build(CommandId.MessageLinkedObject, [message, to, (uint?)mode, selector, p2, p3], [pad0, pad1, pad2, pad3]);

    public static PS2BehaviourCommand ClearUserMessage() =>
        Build(CommandId.ClearUserMessage, [], []);

    public static PS2BehaviourCommand RequestMessSourceAsFocus(AssignTarget? into = null, uint? pad0 = null) =>
        Build(CommandId.RequestMessSourceAsFocus, [(uint?)into], [pad0]);

    public static PS2BehaviourCommand SetCounter(uint counter, ScriptValue value, bool? global = null, uint? @object = null, bool? random = null, bool? allLinked = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetCounter, [counter, Flag(global, true), @object, Flag(random, false), Flag(allLinked, false), value.Word], [pad0, pad1]);

    public static PS2BehaviourCommand ModifyCounter(uint counter, ScriptValue add, bool? global = null, uint? @object = null, bool? allLinked = null, uint? subtype = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.ModifyCounter, [counter, Flag(global, true), @object, Flag(allLinked, false), add.Word, subtype], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand ColliderLaunchNow(float? x = null, float? y = null, float? z = null, uint? space = null, uint? participant = null, Designator? key = null, bool? localVelocity = null, bool? useOffset = null, bool? setDamping = null, bool? setGrip = null, bool? setBounce = null, bool? timer = null, bool? endOnContact = null, bool? ballistic = null, bool? apex = null, bool? aboveTarget = null, bool? state4 = null, float? damping = null, float? grip = null, float? bounce = null, float? timerSeconds = null, float? height = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null) =>
        Build(CommandId.ColliderLaunchNow, [Bits(x), Bits(y), Bits(z), space, participant, (uint?)key, Flag(localVelocity, false), Flag(useOffset, false), Flag(setDamping, false), Flag(setGrip, false), Flag(setBounce, false), Flag(timer, false), Flag(endOnContact, false), Flag(ballistic, false), Flag(apex, false), Flag(aboveTarget, false), Flag(state4, false), Bits(damping), Bits(grip), Bits(bounce), Bits(timerSeconds), Bits(height)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16]);

    public static PS2BehaviourCommand ForceAnimationUpdate(uint? mode = null, uint? pad0 = null) =>
        Build(CommandId.ForceAnimationUpdate, [mode], [pad0]);

    public static PS2BehaviourCommand DestroySpawnedAttachment(uint? exit = null, bool? all = null, uint? pad0 = null) =>
        Build(CommandId.DestroySpawnedAttachment, [exit, Flag(all, false)], [pad0]);

    public static PS2BehaviourCommand ApplyImpulse(Designator? target = null, bool? fromRequester = null, bool? angular = null, bool? linear = null, float? x = null, float? y = null, float? z = null, float? ax = null, float? ay = null, float? az = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.ApplyImpulse, [(uint?)target, Flag(fromRequester, false), Flag(angular, false), Flag(linear, false), Bits(x), Bits(y), Bits(z), Bits(ax), Bits(ay), Bits(az)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand RequestDetach() => Cmd0(CommandId.RequestDetach);

    public static PS2BehaviourCommand SetObject(Toggle? busy = null, Toggle? visible = null, Toggle? collidable = null, Toggle? awake = null, Toggle? bodyFlag20 = null, uint? animRange = null, uint? activityRadius = null, bool? setAnimRange = null, bool? setActivityRadius = null, Toggle? bodyFlag40 = null, Toggle? carries = null, Toggle? solid = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetObject, [(uint?)busy, (uint?)visible, (uint?)collidable, (uint?)awake, (uint?)bodyFlag20, animRange, activityRadius, Flag(setAnimRange, false), Flag(setActivityRadius, false), (uint?)bodyFlag40, (uint?)carries, (uint?)solid], [pad0, pad1]);

    public static PS2BehaviourCommand Keep(bool? trails = null, bool? wobble = null, bool? keep400 = null, bool? keep800 = null, bool? keep1000 = null, bool? sensors = null, uint? pad0 = null) =>
        Build(CommandId.Keep, [Flag(trails, false), Flag(wobble, false), Flag(keep400, false), Flag(keep800, false), Flag(keep1000, false), Flag(sensors, false)], [pad0]);

    public static PS2BehaviourCommand AttachSpring(float stiffness, float damping, float? ownX = null, float? ownY = null, float? ownZ = null, float? pointX = null, float? pointY = null, float? pointZ = null, uint? space = null, uint? hang = null, bool? ownOffset = null, bool? atHolder = null, bool? fixedLength = null, bool? hungNotCollidable = null, uint? holderExit = null, uint? ownExit = null, float? length = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null) =>
        Build(CommandId.AttachSpring, [Bits(ownX), Bits(ownY), Bits(ownZ), Bits(pointX), Bits(pointY), Bits(pointZ), space, hang, Flag(ownOffset, false), Flag(atHolder, false), Flag(fixedLength, false), Flag(hungNotCollidable, false), holderExit, ownExit, Bits(stiffness), Bits(damping), Bits(length)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16]);

    public static PS2BehaviourCommand DetachAllSprings() => Cmd0(CommandId.DetachAllSprings);

    public static PS2BehaviourCommand SetContactSpringy(float? damping = null, float? axisDamping = null, float? inertiaScale = null, float? gravity = null, float? grip = null, float? twist = null, float? bounce = null, float? contactDecel = null, float? springDamping = null, float? springStiffness = null, float? springSide = null, float? centerOfMassX = null, float? centerOfMassY = null, float? centerOfMassZ = null, float? turnToFocus = null, float? constraintX = null, float? constraintY = null, float? constraintZ = null, uint? lockAxes = null, bool? noTerrain = null, uint? dampAxis = null, ConstraintType? constraint = null, uint? substeps = null, bool? forceVolumes = null, bool? neverSleep = null, bool? limitAngle = null, bool? buoyant = null, float? mass = null, float? angleLimit = null, float? contactGrip = null, float? ownerGrip = null, bool? snapToRest = null, uint? mode = null, bool? useCenterOfMass = null, bool? faceVelocity = null, bool? stickOnTouch = null, bool? wobbleOnTouch = null, bool? preWobble = null, bool? messageOnTouch = null, uint? components = null, float? push = null, uint? touchMessage = null, uint? stickMessage = null, uint? stickFilter = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null, uint? pad21 = null, uint? pad22 = null, uint? pad23 = null, uint? pad24 = null, uint? pad25 = null, uint? pad26 = null, uint? pad27 = null, uint? pad28 = null, uint? pad29 = null, uint? pad30 = null, uint? pad31 = null, uint? pad32 = null, uint? pad33 = null) =>
        Build(CommandId.SetContactSpringy, [Bits(damping), Bits(axisDamping), Bits(inertiaScale), Bits(gravity), Bits(grip), Bits(twist), Bits(bounce), Bits(contactDecel), Bits(springDamping), Bits(springStiffness), Bits(springSide), Bits(centerOfMassX), Bits(centerOfMassY), Bits(centerOfMassZ), Bits(turnToFocus), Bits(constraintX), Bits(constraintY), Bits(constraintZ), lockAxes, Flag(noTerrain, false), dampAxis, (uint?)constraint, substeps, Flag(forceVolumes, false), Flag(neverSleep, false), Flag(limitAngle, false), Flag(buoyant, false), Bits(mass), Bits(angleLimit), Bits(contactGrip), Bits(ownerGrip), Flag(snapToRest, false), mode, Flag(useCenterOfMass, false), Flag(faceVelocity, false), Flag(stickOnTouch, false), Flag(wobbleOnTouch, false), Flag(preWobble, false), Flag(messageOnTouch, false), components, Bits(push), touchMessage, stickMessage, stickFilter], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24, pad25, pad26, pad27, pad28, pad29, pad30, pad31, pad32, pad33]);

    public static PS2BehaviourCommand SetContactRigid(float? damping = null, float? axisDamping = null, float? inertiaScale = null, float? gravity = null, float? grip = null, float? twist = null, float? bounce = null, float? contactDecel = null, float? buoyancy = null, float? centerOfMassX = null, float? centerOfMassY = null, float? centerOfMassZ = null, float? turnToFocus = null, float? constraintX = null, float? constraintY = null, float? constraintZ = null, uint? lockAxes = null, bool? noTerrain = null, uint? dampAxis = null, ConstraintType? constraint = null, uint? substeps = null, bool? forceVolumes = null, bool? neverSleep = null, bool? limitAngle = null, bool? buoyant = null, float? mass = null, float? angleLimit = null, float? contactGrip = null, float? ownerGrip = null, bool? snapToRest = null, uint? mode = null, bool? useCenterOfMass = null, bool? faceVelocity = null, bool? stickOnTouch = null, bool? wobbleOnTouch = null, bool? preWobble = null, bool? messageOnTouch = null, uint? components = null, float? push = null, uint? touchMessage = null, uint? stickMessage = null, uint? stickFilter = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null, uint? pad21 = null, uint? pad22 = null, uint? pad23 = null, uint? pad24 = null, uint? pad25 = null, uint? pad26 = null, uint? pad27 = null, uint? pad28 = null, uint? pad29 = null, uint? pad30 = null, uint? pad31 = null, uint? pad32 = null, uint? pad33 = null) =>
        Build(CommandId.SetContactRigid, [Bits(damping), Bits(axisDamping), Bits(inertiaScale), Bits(gravity), Bits(grip), Bits(twist), Bits(bounce), Bits(contactDecel), Bits(buoyancy), Bits(centerOfMassX), Bits(centerOfMassY), Bits(centerOfMassZ), Bits(turnToFocus), Bits(constraintX), Bits(constraintY), Bits(constraintZ), lockAxes, Flag(noTerrain, false), dampAxis, (uint?)constraint, substeps, Flag(forceVolumes, false), Flag(neverSleep, false), Flag(limitAngle, false), Flag(buoyant, false), Bits(mass), Bits(angleLimit), Bits(contactGrip), Bits(ownerGrip), Flag(snapToRest, false), mode, Flag(useCenterOfMass, false), Flag(faceVelocity, false), Flag(stickOnTouch, false), Flag(wobbleOnTouch, false), Flag(preWobble, false), Flag(messageOnTouch, false), components, Bits(push), touchMessage, stickMessage, stickFilter], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24, pad25, pad26, pad27, pad28, pad29, pad30, pad31, pad32, pad33]);

    public static PS2BehaviourCommand ClearContactResponse() => Cmd0(CommandId.ClearContactResponse);

    public static PS2BehaviourCommand DestroyMe(uint? mode = null, uint? pad0 = null) =>
        Build(CommandId.DestroyMe, [mode], [pad0]);

    public static PS2BehaviourCommand SetSound(uint kind, float depth, bool? setDelay = null, bool? setFeedback = null, float? delay = null, float? feedback = null, uint? channel = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null) =>
        Build(CommandId.SetSound, [kind, Flag(setDelay, false), Flag(setFeedback, false), Bits(delay), Bits(feedback), Bits(depth), channel], [pad0, pad1, pad2, pad3, pad4, pad5, pad6]);

    public static PS2BehaviourCommand AlterWobblePhase(uint axes, ScriptValue? x = null, ScriptValue? y = null, ScriptValue? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null) =>
        Build(CommandId.AlterWobblePhase, [x?.Word, y?.Word, z?.Word, axes], [pad0, pad1, pad2, pad3]);

    public static PS2BehaviourCommand BeginMusic(ScriptValue track, uint channel, float volume, bool? loop = null, float? time = null, float? emitterRange = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.BeginMusic, [track.Word, channel, Flag(loop, false), Bits(volume), Bits(time), Bits(emitterRange)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand EndContextMusic(float seconds, uint? pad0 = null) =>
        Build(CommandId.EndContextMusic, [Bits(seconds)], [pad0]);

    public static PS2BehaviourCommand SetFocusToAgent2(Designator agent, AssignTarget? into = null, uint? pad0 = null) =>
        Build(CommandId.SetFocusToAgent2, [(uint?)agent, (uint?)into], [pad0]);

    public static PS2BehaviourCommand SetFocusToAgent3(Designator agent, AssignTarget? into = null, uint? pad0 = null) =>
        Build(CommandId.SetFocusToAgent3, [(uint?)agent, (uint?)into], [pad0]);

    public static PS2BehaviourCommand AttachFocusObject2(AttachSource? source = null, uint? exit = null, uint? mode = null, bool? hasOffset = null, bool? hasRotation = null, uint? link = null, bool? busy = null, bool? reverse = null, float? x = null, float? y = null, float? z = null, int? rx = null, int? ry = null, int? rz = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.AttachFocusObject2, [(uint?)source, exit, mode, Flag(hasOffset, false), Flag(hasRotation, false), link, Flag(busy, false), Flag(reverse, false), Bits(x), Bits(y), Bits(z), Int(rx), Int(ry), Int(rz)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand ReleaseCached(uint flags, uint? pad0 = null) =>
        Build(CommandId.ReleaseCached, [flags], [pad0]);

    public static PS2BehaviourCommand LaunchCached(float? offsetX = null, float? offsetY = null, float? offsetZ = null, float? damping = null, float? axisDamping = null, float? inertiaScale = null, float? gravity = null, float? grip = null, float? twist = null, float? bounce = null, float? contactDecel = null, float? buoyancy = null, float? centerOfMassX = null, float? centerOfMassY = null, float? centerOfMassZ = null, float? turnToFocus = null, float? constraintX = null, float? constraintY = null, float? constraintZ = null, uint? lockAxes = null, bool? noTerrain = null, uint? dampAxis = null, ConstraintType? constraint = null, uint? substeps = null, bool? forceVolumes = null, bool? neverSleep = null, bool? limitAngle = null, bool? buoyant = null, float? mass = null, float? angleLimit = null, float? contactGrip = null, float? ownerGrip = null, bool? snapToRest = null, uint? mode = null, bool? useCenterOfMass = null, bool? faceVelocity = null, bool? stickOnTouch = null, bool? wobbleOnTouch = null, bool? preWobble = null, bool? messageOnTouch = null, uint? components = null, float? push = null, uint? touchMessage = null, uint? stickMessage = null, uint? stickFilter = null, uint? space = null, uint? participant = null, Designator? key = null, bool? useOffset = null, bool? jitter = null, bool? clearBusy = null, bool? passSlot = null, ScriptValue? speed = null, float? jitterRadius = null, float? spinX = null, float? spinY = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null, uint? pad21 = null, uint? pad22 = null, uint? pad23 = null, uint? pad24 = null, uint? pad25 = null, uint? pad26 = null, uint? pad27 = null, uint? pad28 = null, uint? pad29 = null, uint? pad30 = null, uint? pad31 = null, uint? pad32 = null, uint? pad33 = null, uint? pad34 = null, uint? pad35 = null, uint? pad36 = null, uint? pad37 = null, uint? pad38 = null, uint? pad39 = null, uint? pad40 = null, uint? pad41 = null, uint? pad42 = null, uint? pad43 = null, uint? pad44 = null) =>
        Build(CommandId.LaunchCached, [Bits(offsetX), Bits(offsetY), Bits(offsetZ), Bits(damping), Bits(axisDamping), Bits(inertiaScale), Bits(gravity), Bits(grip), Bits(twist), Bits(bounce), Bits(contactDecel), Bits(buoyancy), Bits(centerOfMassX), Bits(centerOfMassY), Bits(centerOfMassZ), Bits(turnToFocus), Bits(constraintX), Bits(constraintY), Bits(constraintZ), lockAxes, Flag(noTerrain, false), dampAxis, (uint?)constraint, substeps, Flag(forceVolumes, false), Flag(neverSleep, false), Flag(limitAngle, false), Flag(buoyant, false), Bits(mass), Bits(angleLimit), Bits(contactGrip), Bits(ownerGrip), Flag(snapToRest, false), mode, Flag(useCenterOfMass, false), Flag(faceVelocity, false), Flag(stickOnTouch, false), Flag(wobbleOnTouch, false), Flag(preWobble, false), Flag(messageOnTouch, false), components, Bits(push), touchMessage, stickMessage, stickFilter, space, participant, (uint?)key, Flag(useOffset, false), Flag(jitter, false), Flag(clearBusy, false), Flag(passSlot, false), speed?.Word, Bits(jitterRadius), Bits(spinX), Bits(spinY)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24, pad25, pad26, pad27, pad28, pad29, pad30, pad31, pad32, pad33, pad34, pad35, pad36, pad37, pad38, pad39, pad40, pad41, pad42, pad43, pad44]);

    public static PS2BehaviourCommand ClearSlot() => Cmd0(CommandId.ClearSlot);

    public static PS2BehaviourCommand ClearSecondSlot() => Cmd0(CommandId.ClearSecondSlot);

    public static PS2BehaviourCommand RequestMessSourceAsFocus2(AssignTarget? into = null, uint? pad0 = null) =>
        Build(CommandId.RequestMessSourceAsFocus2, [(uint?)into], [pad0]);

    public static PS2BehaviourCommand RequestMessSourceAsFocus3(AssignTarget? into = null, uint? pad0 = null) =>
        Build(CommandId.RequestMessSourceAsFocus3, [(uint?)into], [pad0]);

    public static PS2BehaviourCommand SetFocusToKey2(uint key, AssignTarget? into = null, bool? ofFocus = null, bool? ofSlot = null, uint? pad0 = null) =>
        Build(CommandId.SetFocusToKey2, [key, (uint?)into, Flag(ofFocus, false), Flag(ofSlot, false)], [pad0]);

    public static PS2BehaviourCommand SetFocusToAgent4(Designator agent, AssignTarget? into = null, uint? pad0 = null) =>
        Build(CommandId.SetFocusToAgent4, [(uint?)agent, (uint?)into], [pad0]);

    public static PS2BehaviourCommand SetRotation2(Designator key, uint? participant = null, uint? space = null, bool? useOffset = null, bool? face = null, bool? body = null, bool? water = null, float? x = null, float? y = null, float? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.SetRotation2, [participant, (uint?)key, space, Flag(useOffset, false), Flag(face, false), Flag(body, false), Flag(water, false), Bits(x), Bits(y), Bits(z)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand RequestMessengersFocus4(AssignTarget? storeIn = null, uint? pad0 = null) =>
        Build(CommandId.RequestMessengersFocus4, [(uint?)storeIn], [pad0]);

    public static PS2BehaviourCommand SetFocusPosition2(Designator key, float? x = null, float? y = null, float? z = null, uint? participant = null, uint? space = null, bool? useOffset = null, bool? jitter = null, AssignTarget? into = null, bool? projectX = null, bool? projectY = null, bool? projectZ = null, bool? initialFrame = null, bool? relative = null, bool? focusJoint = null, bool? forward = null, float? jitterScale = null, float? jitterY = null, float? routeBisect = null, float? routeRandom = null, float? sensorDistance = null, float? awayFromPlayer = null, float? routeSide = null, float? routeUp = null, float? routeToward = null, uint? joint = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null) =>
        Build(CommandId.SetFocusPosition2, [Bits(x), Bits(y), Bits(z), participant, (uint?)key, space, Flag(useOffset, false), Flag(jitter, false), (uint?)into, Flag(projectX, false), Flag(projectY, false), Flag(projectZ, false), Flag(initialFrame, false), Flag(relative, false), Flag(focusJoint, false), Flag(forward, false), Bits(jitterScale), Bits(jitterY), Bits(routeBisect), Bits(routeRandom), Bits(sensorDistance), Bits(awayFromPlayer), Bits(routeSide), Bits(routeUp), Bits(routeToward), joint], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20]);

    public static PS2BehaviourCommand AddNoiseToFocusPos2(ScriptValue radius, AssignTarget? into = null, float? x = null, float? y = null, float? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.AddNoiseToFocusPos2, [(uint?)into, radius.Word, Bits(x), Bits(y), Bits(z)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand ClearStoredPosition() => Cmd0(CommandId.ClearStoredPosition);

    public static PS2BehaviourCommand CacheLinkedInstance() => Cmd0(CommandId.CacheLinkedInstance);

    public static PS2BehaviourCommand SpawnCachedResidentAgent(float? x = null, float? y = null, float? z = null, ScriptValue? subtype = null, uint? objectId = null, Designator? key = null, uint? activityRadius = null, uint? message = null, uint? joint = null, uint? space = null, bool? flag29 = null, bool? useOffset = null, bool? attach = null, bool? link = null, bool? atFocusPosition = null, bool? atFocusObject = null, bool? setRotation = null, bool? copySelf = null, bool? focusOnSpawned = null, bool? toSecondSlot = null, bool? copyStateSlot = null, bool? copyCached = null, bool? linkBack = null, bool? passSlot = null, bool? passCached = null, bool? passFocus = null, bool? passStored = null, bool? linkAll = null, bool? setSubtype = null, bool? atStored = null, bool? busy = null, bool? notBusy = null, bool? inheritKeys = null, bool? inheritPaths = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.SpawnCachedResidentAgent, [Bits(x), Bits(y), Bits(z), subtype?.Word, objectId, (uint?)key, activityRadius, message, joint, space, Flag(flag29, false), Flag(useOffset, false), Flag(attach, false), Flag(link, false), Flag(atFocusPosition, false), Flag(atFocusObject, false), Flag(setRotation, false), Flag(copySelf, false), Flag(focusOnSpawned, false), Flag(toSecondSlot, false), Flag(copyStateSlot, false), Flag(copyCached, false), Flag(linkBack, false), Flag(passSlot, false), Flag(passCached, false), Flag(passFocus, false), Flag(passStored, false), Flag(linkAll, false), Flag(setSubtype, false), Flag(atStored, false), Flag(busy, false), Flag(notBusy, false), Flag(inheritKeys, false), Flag(inheritPaths, false)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand StopLookAt() => Cmd0(CommandId.StopLookAt);

    public static PS2BehaviourCommand StartLookAt() => Cmd0(CommandId.StartLookAt);

    public static PS2BehaviourCommand DeleteLookAt() => Cmd0(CommandId.DeleteLookAt);

    public static PS2BehaviourCommand ApplyBlastRadius(float radius, float strength, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.ApplyBlastRadius, [Bits(radius), Bits(strength)], [pad0, pad1]);

    public static PS2BehaviourCommand SetLookTarget(float weight, uint? participant = null, bool? player = null, bool? secondSlot = null, bool? focus = null, bool? persistent = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetLookTarget, [participant, Flag(player, false), Flag(secondSlot, false), Flag(focus, false), Flag(persistent, false), Bits(weight)], [pad0, pad1]);

    public static PS2BehaviourCommand ClearImpact() => Cmd0(CommandId.ClearImpact);

    public static PS2BehaviourCommand PhysicsResetVelocity() => Cmd0(CommandId.PhysicsResetVelocity);

    public static PS2BehaviourCommand FocusBesidePlayer(float distance, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.FocusBesidePlayer, [Bits(distance)], [pad0, pad1]);

    public static PS2BehaviourCommand CopyDesignator(Designator from, Designator to, Designator? owner = null, uint? pad0 = null) =>
        Build(CommandId.CopyDesignator, [(uint?)from, (uint?)to, (uint?)owner], [pad0]);

    public static PS2BehaviourCommand LinkToNearestPoint() => Cmd0(CommandId.LinkToNearestPoint);

    public static PS2BehaviourCommand RunScriptSlot(uint slot, uint? pad0 = null) =>
        Build(CommandId.RunScriptSlot, [slot], [pad0]);

    public static PS2BehaviourCommand OffsetFocusPosition(float y, uint? mask = null, float? x = null, float? z = null, float? w = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.OffsetFocusPosition, [mask, Bits(x), Bits(y), Bits(z), Bits(w)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand NextLink() => Cmd0(CommandId.NextLink);

    public static PS2BehaviourCommand SetRigidBodyGravity(float gravity, uint? pad0 = null) =>
        Build(CommandId.SetRigidBodyGravity, [Bits(gravity)], [pad0]);

    public static PS2BehaviourCommand ActivatePhysics() => Cmd0(CommandId.ActivatePhysics);

    public static PS2BehaviourCommand MagnetPullToFocus() => Cmd0(CommandId.MagnetPullToFocus);

    public static PS2BehaviourCommand SetLinkCursor(uint index, uint? pad0 = null) =>
        Build(CommandId.SetLinkCursor, [index], [pad0]);

    public static PS2BehaviourCommand ClearDamage() => Cmd0(CommandId.ClearDamage);

    public static PS2BehaviourCommand FocusOnRequester() => Cmd0(CommandId.FocusOnRequester);

    public static PS2BehaviourCommand SetSensor(uint sensor, float value, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetSensor, [sensor, Bits(value)], [pad0, pad1]);

    public static PS2BehaviourCommand SetInfluence(float influence, uint? pad0 = null) =>
        Build(CommandId.SetInfluence, [Bits(influence)], [pad0]);

    public static PS2BehaviourCommand SetParentLevelValue(uint value, uint? pad0 = null) =>
        Build(CommandId.SetParentLevelValue, [value], [pad0]);

    public static PS2BehaviourCommand FocusOnLinked(uint item, bool? fromLinks = null, bool? atCursor = null, bool? ofFocus = null, bool? ofSlot = null, AssignTarget? into = null, bool? last = null, uint? pad0 = null) =>
        Build(CommandId.FocusOnLinked, [item, Flag(fromLinks, false), Flag(atCursor, false), Flag(ofFocus, false), Flag(ofSlot, false), (uint?)into, Flag(last, false)], [pad0]);

    public static PS2BehaviourCommand PrevKey() => Cmd0(CommandId.PrevKey);

    public static PS2BehaviourCommand SetWobbleAmplitude(uint axes, ScriptValue? x = null, ScriptValue? y = null, ScriptValue? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null) =>
        Build(CommandId.SetWobbleAmplitude, [x?.Word, y?.Word, z?.Word, axes], [pad0, pad1, pad2, pad3]);

    public static PS2BehaviourCommand SpinWithParent(float speed, uint flags, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SpinWithParent, [Bits(speed), flags], [pad0, pad1]);

    public static PS2BehaviourCommand StrafeTowardsTarget(uint flags, float distance, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.StrafeTowardsTarget, [flags, Bits(distance)], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand NextRouteStep() => Cmd0(CommandId.NextRouteStep);

    public static PS2BehaviourCommand StoreLinkedPosition(uint item, bool? fromLinks = null, bool? atCursor = null, bool? ofFocus = null, bool? ofSlot = null, AssignTarget? into = null, bool? last = null, uint? pad0 = null) =>
        Build(CommandId.StoreLinkedPosition, [item, Flag(fromLinks, false), Flag(atCursor, false), Flag(ofFocus, false), Flag(ofSlot, false), (uint?)into, Flag(last, false)], [pad0]);

    public static PS2BehaviourCommand SetSlotToLinked(uint item, bool? fromLinks = null, bool? atCursor = null, bool? ofFocus = null, bool? ofSlot = null, AssignTarget? into = null, bool? last = null, uint? pad0 = null) =>
        Build(CommandId.SetSlotToLinked, [item, Flag(fromLinks, false), Flag(atCursor, false), Flag(ofFocus, false), Flag(ofSlot, false), (uint?)into, Flag(last, false)], [pad0]);

    public static PS2BehaviourCommand SetTouchMessage(uint? message = null, float? freqX = null, float? freqY = null, float? freqZ = null, float? ampX = null, float? ampY = null, float? ampZ = null, float? phaseX = null, float? phaseY = null, float? phaseZ = null, float? stiffness = null, float? rate = null, float? duration = null, uint? rest = null, uint? space = null, WobbleWave? waveX = null, WobbleWave? waveY = null, WobbleWave? waveZ = null, bool? rotate = null, bool? fade = null, bool? randomPhaseX = null, bool? randomPhaseY = null, bool? randomPhaseZ = null, uint? signX = null, uint? signY = null, uint? signZ = null, uint? follow = null, bool? axisX = null, bool? axisY = null, bool? axisZ = null, bool? easeIn = null, float? contactGrip = null, float? ownerGrip = null, bool? snapToRest = null, uint? mode = null, bool? useCenterOfMass = null, bool? faceVelocity = null, bool? stickOnTouch = null, bool? wobbleOnTouch = null, bool? preWobble = null, bool? messageOnTouch = null, uint? components = null, float? push = null, uint? touchMessage = null, uint? stickMessage = null, uint? stickFilter = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null, uint? pad21 = null, uint? pad22 = null, uint? pad23 = null, uint? pad24 = null, uint? pad25 = null, uint? pad26 = null, uint? pad27 = null, uint? pad28 = null, uint? pad29 = null, uint? pad30 = null, uint? pad31 = null, uint? pad32 = null, uint? pad33 = null, uint? pad34 = null) =>
        Build(CommandId.SetTouchMessage, [message, Bits(freqX), Bits(freqY), Bits(freqZ), Bits(ampX), Bits(ampY), Bits(ampZ), Bits(phaseX), Bits(phaseY), Bits(phaseZ), Bits(stiffness), Bits(rate), Bits(duration), rest, space, (uint?)waveX, (uint?)waveY, (uint?)waveZ, Flag(rotate, false), Flag(fade, false), Flag(randomPhaseX, false), Flag(randomPhaseY, false), Flag(randomPhaseZ, false), signX, signY, signZ, follow, Flag(axisX, false), Flag(axisY, false), Flag(axisZ, false), Flag(easeIn, false), Bits(contactGrip), Bits(ownerGrip), Flag(snapToRest, false), mode, Flag(useCenterOfMass, false), Flag(faceVelocity, false), Flag(stickOnTouch, false), Flag(wobbleOnTouch, false), Flag(preWobble, false), Flag(messageOnTouch, false), components, Bits(push), touchMessage, stickMessage, stickFilter], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24, pad25, pad26, pad27, pad28, pad29, pad30, pad31, pad32, pad33, pad34]);

    public static PS2BehaviourCommand ClearTouchMessage() => Cmd0(CommandId.ClearTouchMessage);

    public static PS2BehaviourCommand ModifyFocusCounter(uint counter, ScriptValue value, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.ModifyFocusCounter, [counter, value.Word], [pad0, pad1]);

    public static PS2BehaviourCommand UnlinkFromTarget(Designator target, bool? allLinks = null, bool? byIndex = null, bool? atCursor = null, bool? force = null, uint? pad0 = null) =>
        Build(CommandId.UnlinkFromTarget, [(uint?)target, Flag(allLinks, false), Flag(byIndex, false), Flag(atCursor, false), Flag(force, false)], [pad0]);

    public static PS2BehaviourCommand AddToLinkedObjectsByte(uint filterAndIndex, ScriptValue value, uint? select = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.AddToLinkedObjectsByte, [filterAndIndex, select, value.Word], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand ForceVolumeController(ScriptValue? fx = null, ScriptValue? fy = null, ScriptValue? fz = null, bool? turnOn = null, bool? turnOff = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null) =>
        Build(CommandId.ForceVolumeController, [fx?.Word, fy?.Word, fz?.Word, Flag(turnOn, false), Flag(turnOff, false)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12]);

    public static PS2BehaviourCommand TouchAround(ScriptValue half, uint? pad0 = null) =>
        Build(CommandId.TouchAround, [half.Word], [pad0]);

    public static PS2BehaviourCommand SetSurface(uint surface, bool? allHulls = null, uint? hull = null, uint? pad0 = null) =>
        Build(CommandId.SetSurface, [surface, Flag(allHulls, false), hull], [pad0]);

    public static PS2BehaviourCommand PushBodiesInSphere(float radius, float strength, float innerStrength, uint flags, float? x = null, float? y = null, float? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.PushBodiesInSphere, [Bits(x), Bits(y), Bits(z), Bits(radius), Bits(strength), Bits(innerStrength), flags], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand PointToward(uint w, float distance, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.PointToward, [w, Bits(distance)], [pad0, pad1]);

    public static PS2BehaviourCommand SetFocusCounter(uint counter, ScriptValue value, uint? from = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.SetFocusCounter, [counter, from, value.Word], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand RequestOnTarget(Designator target, uint slot, uint? objectId = null, uint? channel = null, bool? allLinks = null, bool? control = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.RequestOnTarget, [(uint?)target, objectId, slot, channel, Flag(allLinks, false), Flag(control, false)], [pad0, pad1]);

    public static PS2BehaviourCommand ReleaseTarget(Designator target, bool? allLinks = null, uint? pad0 = null) =>
        Build(CommandId.ReleaseTarget, [(uint?)target, Flag(allLinks, false)], [pad0]);

    public static PS2BehaviourCommand SetPathIndex(uint index, uint? pad0 = null) =>
        Build(CommandId.SetPathIndex, [index], [pad0]);

    public static PS2BehaviourCommand SetFocusToOwner() => Cmd0(CommandId.SetFocusToOwner);

    public static PS2BehaviourCommand EndMusic(float seconds, uint channel, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.EndMusic, [Bits(seconds), channel], [pad0, pad1]);

    public static PS2BehaviourCommand WarpAgent(Designator key, Designator agent, uint? participant = null, uint? space = null, bool? useOffset = null, bool? face = null, bool? body = null, Designator? source = null, float? x = null, float? y = null, float? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.WarpAgent, [participant, (uint?)key, space, Flag(useOffset, false), Flag(face, false), Flag(body, false), (uint?)agent, (uint?)source, Bits(x), Bits(y), Bits(z)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand RotateAgent(uint flags, uint source, float? x = null, float? y = null, float? z = null, float? w = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.RotateAgent, [flags, source, Bits(x), Bits(y), Bits(z), Bits(w)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand WarpAgentFacing(Designator key, Designator agent, uint? participant = null, uint? space = null, bool? useOffset = null, bool? face = null, bool? body = null, Designator? source = null, float? x = null, float? y = null, float? z = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.WarpAgentFacing, [participant, (uint?)key, space, Flag(useOffset, false), Flag(face, false), Flag(body, false), (uint?)agent, (uint?)source, Bits(x), Bits(y), Bits(z)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand TakeControl(Designator target, bool? own = null, uint? pad0 = null) =>
        Build(CommandId.TakeControl, [(uint?)target, Flag(own, false)], [pad0]);

    public static PS2BehaviourCommand ReleaseControl(Designator target, bool? own = null, uint? pad0 = null) =>
        Build(CommandId.ReleaseControl, [(uint?)target, Flag(own, false)], [pad0]);

    public static PS2BehaviourCommand PrepareSpeech(ScriptValue track, bool? loop = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.PrepareSpeech, [track.Word, Flag(loop, false)], [pad0, pad1]);

    public static PS2BehaviourCommand PlaySpeech() => Cmd0(CommandId.PlaySpeech);

    public static PS2BehaviourCommand SetShadow(ScriptValue maxDistance, ScriptValue lengthNear, uint? slot = null, ScriptValue? lengthFar = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null) =>
        Build(CommandId.SetShadow, [slot, maxDistance.Word, lengthNear.Word, lengthFar?.Word], [pad0, pad1, pad2, pad3]);

    public static PS2BehaviourCommand SetShadowRectangle(ScriptValue sizeX, ScriptValue sizeZ, ScriptValue offsetY, uint? slot = null, uint? type = null, ScriptValue? offsetX = null, ScriptValue? offsetZ = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.SetShadowRectangle, [slot, type, sizeX.Word, sizeZ.Word, offsetX?.Word, offsetY.Word, offsetZ?.Word], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand ShadowToggle(uint slot, uint? pad0 = null) =>
        Build(CommandId.ShadowToggle, [slot], [pad0]);

    public static PS2BehaviourCommand LaunchSelf(float height, float? x = null, float? y = null, float? z = null, uint? space = null, uint? participant = null, Designator? key = null, bool? localVelocity = null, bool? useOffset = null, bool? ballistic = null, bool? apex = null, bool? aboveTarget = null, float? spinX = null, float? spinY = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.LaunchSelf, [Bits(x), Bits(y), Bits(z), space, participant, (uint?)key, Flag(localVelocity, false), Flag(useOffset, false), Flag(ballistic, false), Flag(apex, false), Flag(aboveTarget, false), Bits(height), Bits(spinX), Bits(spinY)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand SetContactSounds(uint firstSound, uint lastSound, bool? noSurfaceSound = null, float? threshold = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetContactSounds, [firstSound, lastSound, Flag(noSurfaceSound, false), Bits(threshold)], [pad0, pad1]);

    public static PS2BehaviourCommand StopSpeech() => Cmd0(CommandId.StopSpeech);

    public static PS2BehaviourCommand StopStoredSound() => Cmd0(CommandId.StopStoredSound);

    public static PS2BehaviourCommand SetCollisionBoxSize(float sx, float sy, float sz, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.SetCollisionBoxSize, [Bits(sx), Bits(sy), Bits(sz)], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand DestroyInstanceInBox() => Cmd0(CommandId.DestroyInstanceInBox);

    public static PS2BehaviourCommand SetFocusPositionOffset(float? x = null, float? y = null, float? z = null, float? side = null, float? up = null, float? back = null, float? distance = null, bool? setX = null, bool? setY = null, bool? setZ = null, bool? addSide = null, bool? addUp = null, bool? addBack = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null) =>
        Build(CommandId.SetFocusPositionOffset, [Bits(x), Bits(y), Bits(z), Bits(side), Bits(up), Bits(back), Bits(distance), Flag(setX, false), Flag(setY, false), Flag(setZ, false), Flag(addSide, false), Flag(addUp, false), Flag(addBack, false)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7]);

    public static PS2BehaviourCommand SetFocusPositionAtAngle(float distance, float angle, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetFocusPositionAtAngle, [Bits(distance), Bits(angle)], [pad0, pad1]);

    public static PS2BehaviourCommand LockLevels() => Cmd0(CommandId.LockLevels);

    public static PS2BehaviourCommand UnlockLevels() => Cmd0(CommandId.UnlockLevels);

    public static PS2BehaviourCommand MarkTime() => Cmd0(CommandId.MarkTime);

    public static PS2BehaviourCommand ClearMarkedTime() => Cmd0(CommandId.ClearMarkedTime);

    public static PS2BehaviourCommand RestartRoute() => Cmd0(CommandId.RestartRoute);

    public static PS2BehaviourCommand ControllerRumble(float strength, uint? pad0 = null) =>
        Build(CommandId.ControllerRumble, [Bits(strength)], [pad0]);

    public static PS2BehaviourCommand ModifyStoredSound(float pitch, bool? setPitch = null, bool? setVolume = null, float? volume = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.ModifyStoredSound, [Flag(setPitch, false), Flag(setVolume, false), Bits(pitch), Bits(volume)], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand CreateCrateContents(uint contents, uint flags, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.CreateCrateContents, [contents, flags], [pad0, pad1]);

    public static PS2BehaviourCommand CA_PickUpWumpa(int count, uint? pad0 = null) =>
        Build(CommandId.CA_PickUpWumpa, [Int(count)], [pad0]);

    public static PS2BehaviourCommand CreateDamage(uint kind, ScriptValue amount, uint shape, ScriptValue radius, float? x = null, float? y = null, float? z = null, bool? useStrength = null, bool? touch = null, bool? useOffset = null, bool? noCharacters = null, bool? charactersOnly = null, uint? joint = null, bool? nearestOnly = null, ScriptValue? strength = null, uint? selector = null, ScriptValue? height = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null) =>
        Build(CommandId.CreateDamage, [Bits(x), Bits(y), Bits(z), Flag(useStrength, false), Flag(touch, false), Flag(useOffset, false), Flag(noCharacters, false), Flag(charactersOnly, false), joint, Flag(nearestOnly, false), kind, amount.Word, strength?.Word, shape, selector, radius.Word, height?.Word], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12]);

    public static PS2BehaviourCommand SetAgent(uint change, uint? value = null, uint? pad0 = null) =>
        Build(CommandId.SetAgent, [change, value], [pad0]);

    public static PS2BehaviourCommand SetPlayerRespawnPosition(uint? levelRespawn = null, uint? pad0 = null) =>
        Build(CommandId.SetPlayerRespawnPosition, [levelRespawn], [pad0]);

    public static PS2BehaviourCommand ResetGame() => Cmd0(CommandId.ResetGame);

    public static PS2BehaviourCommand SetCrate(uint crate, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetCrate, [crate], [pad0, pad1]);

    public static PS2BehaviourCommand TriggerBalancedCrateFalling() => Cmd0(CommandId.TriggerBalancedCrateFalling);

    public static PS2BehaviourCommand CA_PickUpHealth() => Cmd0(CommandId.CA_PickUpHealth);

    public static PS2BehaviourCommand SetPlayerInput(uint change, uint? off = null, uint? flags = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetPlayerInput, [change, off, flags], [pad0, pad1]);

    public static PS2BehaviourCommand TriggerAllNitroCrates() => Cmd0(CommandId.TriggerAllNitroCrates);

    public static PS2BehaviourCommand ApplyVelocity(ScriptValue speed, uint mode, uint target, ScriptValue? vx = null, ScriptValue? vy = null, ScriptValue? vz = null, ScriptValue? dx = null, ScriptValue? dy = null, ScriptValue? dz = null, ScriptValue? v8 = null, ScriptValue? v9 = null, ScriptValue? v10 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null) =>
        Build(CommandId.ApplyVelocity, [speed.Word, vx?.Word, vy?.Word, vz?.Word, dx?.Word, dy?.Word, dz?.Word, mode, v8?.Word, v9?.Word, v10?.Word, target], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11]);

    public static PS2BehaviourCommand SetKeyNearestPlayer(float? range = null, float? lead = null, uint? includeCurrent = null, uint? first = null, uint? last = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.SetKeyNearestPlayer, [Bits(range), Bits(lead), includeCurrent, first, last], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand RaycastFocusPosition(float? x = null, float? y = null, float? z = null, float? back = null, uint? frame = null, Designator? source = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null) =>
        Build(CommandId.RaycastFocusPosition, [Bits(x), Bits(y), Bits(z), Bits(back), frame, (uint?)source], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12]);

    public static PS2BehaviourCommand SetChiChiGrass(uint value, uint? pad0 = null) =>
        Build(CommandId.SetChiChiGrass, [value], [pad0]);

    public static PS2BehaviourCommand ReduceHitPoints(int amount, uint? pad0 = null) =>
        Build(CommandId.ReduceHitPoints, [Int(amount)], [pad0]);

    public static PS2BehaviourCommand SetHitPoints(int hitPoints, uint? pad0 = null) =>
        Build(CommandId.SetHitPoints, [Int(hitPoints)], [pad0]);

    public static PS2BehaviourCommand RestartDefault() => Cmd0(CommandId.RestartDefault);

    public static PS2BehaviourCommand NowGoBackCollidable(ScriptValue scale, uint? pad0 = null) =>
        Build(CommandId.NowGoBackCollidable, [scale.Word], [pad0]);

    public static PS2BehaviourCommand SetGlobalProgression(int? value = null, ScriptValue? fromValue = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetGlobalProgression, [Int(value), fromValue?.Word], [pad0, pad1]);

    public static PS2BehaviourCommand AddCrystal() => Cmd0(CommandId.AddCrystal);

    public static PS2BehaviourCommand AddGem(int gem, uint? pad0 = null) =>
        Build(CommandId.AddGem, [Int(gem)], [pad0]);

    public static PS2BehaviourCommand CA_ClearTrail() => Cmd0(CommandId.CA_ClearTrail);

    public static PS2BehaviourCommand CA_DoAnimation(ScriptValue blend, byte[]? slots = null, uint? flags = null, ScriptValue? p2 = null, ScriptValue? p3 = null, float? p4 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.CA_DoAnimation, [slots, blend.Word, flags, p2?.Word, p3?.Word, Bits(p4)], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand CA_DoParticle(uint system, uint? count = null, bool? attached = null, bool? attached2 = null, uint? frame = null, bool? local = null, uint? axis = null, bool? useOffset = null, Designator? target = null, uint? actorOverride = null, uint? key = null, float? x = null, float? y = null, float? z = null, float? w = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.CA_DoParticle, [system, count, Flag(attached, false), Flag(attached2, false), frame, Flag(local, false), axis, Flag(useOffset, false), (uint?)target, actorOverride, key, Bits(x), Bits(y), Bits(z), Bits(w)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand Shoot(uint projectile, uint flags, float? x = null, float? y = null, float? z = null, float? w = null, float? speed = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null) =>
        Build(CommandId.Shoot, [Bits(x), Bits(y), Bits(z), Bits(w), projectile, flags, Bits(speed)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8]);

    public static PS2BehaviourCommand GetShortRoute(Designator key, float? x = null, float? y = null, float? z = null, uint? participant = null, bool? flag16 = null, bool? limitDirections = null, bool? noDirection80000 = null, bool? noDirection40000 = null, bool? noFlag100000 = null, bool? flag21 = null, bool? flag2 = null, bool? nearFocus = null, bool? preferMiddle = null, bool? useOffset = null, bool? simple = null, bool? avoidCrowds = null, uint? space = null, bool? ahead = null, bool? noFlag200000 = null, bool? noFlag400000 = null, bool? noFlag800000 = null, bool? noFlag1000000 = null, ScriptValue? middleWeight = null, ScriptValue? nearFocusWeight = null, ScriptValue? value9 = null, ScriptValue? crowdWeight = null, ScriptValue? aheadDistance = null, uint? p12 = null, uint? toNeed = null, uint? toSkip = null, uint? fromNeed = null, uint? fromSkip = null, float? fromMaxDistance = null, float? toMaxDistance = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null) =>
        Build(CommandId.GetShortRoute, [Bits(x), Bits(y), Bits(z), participant, (uint?)key, Flag(flag16, false), Flag(limitDirections, false), Flag(noDirection80000, false), Flag(noDirection40000, false), Flag(noFlag100000, false), Flag(flag21, false), Flag(flag2, false), Flag(nearFocus, false), Flag(preferMiddle, false), Flag(useOffset, false), Flag(simple, false), Flag(avoidCrowds, false), space, Flag(ahead, false), Flag(noFlag200000, false), Flag(noFlag400000, false), Flag(noFlag800000, false), Flag(noFlag1000000, false), middleWeight?.Word, nearFocusWeight?.Word, value9?.Word, crowdWeight?.Word, aheadDistance?.Word, p12, toNeed, toSkip, fromNeed, fromSkip, Bits(fromMaxDistance), Bits(toMaxDistance)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16]);

    public static PS2BehaviourCommand DetachAllLinkedAgents() => Cmd0(CommandId.DetachAllLinkedAgents);

    public static PS2BehaviourCommand SetVehicleHumiliskate(uint driver, uint rider, ScriptValue maxSpeed, ScriptValue crouchSpeed, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.SetVehicleHumiliskate, [driver, rider, maxSpeed.Word, crouchSpeed.Word], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand SetFocusToPlayer() => Cmd0(CommandId.SetFocusToPlayer);

    public static PS2BehaviourCommand RequestFocus(float? offsetX = null, float? offsetY = null, float? offsetZ = null, uint? object1 = null, uint? object2 = null, uint? object3 = null, uint? object4 = null, uint? heldObject1 = null, uint? heldObject2 = null, uint? space = null, uint? participant = null, Designator? key = null, uint? objectCount = null, bool? nearest = null, bool? useOffset = null, bool? pickRandom = null, uint? attachFilter = null, uint? busyFilter = null, bool? markBusy = null, bool? alsoSecondSlot = null, AssignTarget? storeIn = null, bool? includeNonReceivers = null, uint? heldCount = null, bool? allowCurrent = null, bool? visibleOnly = null, uint? components = null, float? radius = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null) =>
        Build(CommandId.RequestFocus, [Bits(offsetX), Bits(offsetY), Bits(offsetZ), object1, object2, object3, object4, heldObject1, heldObject2, space, participant, (uint?)key, objectCount, Flag(nearest, false), Flag(useOffset, false), Flag(pickRandom, false), attachFilter, busyFilter, Flag(markBusy, false), Flag(alsoSecondSlot, false), (uint?)storeIn, Flag(includeNonReceivers, false), heldCount, Flag(allowCurrent, false), Flag(visibleOnly, false), components, Bits(radius)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12]);

    public static PS2BehaviourCommand SetFocusProperties(uint properties, uint? pad0 = null) =>
        Build(CommandId.SetFocusProperties, [properties], [pad0]);

    public static PS2BehaviourCommand SetCamera(uint angle, uint? flags = null, float? value = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.SetCamera, [flags, angle, Bits(value)], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand RestoreCameraDefaults() => Cmd0(CommandId.RestoreCameraDefaults);

    public static PS2BehaviourCommand LinkToFocusCharacter(uint? mode = null, uint? pad0 = null) =>
        Build(CommandId.LinkToFocusCharacter, [mode], [pad0]);

    public static PS2BehaviourCommand UnlinkCharacters() => Cmd0(CommandId.UnlinkCharacters);

    public static PS2BehaviourCommand RequestFocus2(float? offsetX = null, float? offsetY = null, float? offsetZ = null, uint? object1 = null, uint? object2 = null, uint? object3 = null, uint? object4 = null, uint? heldObject1 = null, uint? heldObject2 = null, uint? space = null, uint? participant = null, Designator? key = null, uint? objectCount = null, bool? nearest = null, bool? useOffset = null, bool? pickRandom = null, uint? attachFilter = null, uint? busyFilter = null, bool? markBusy = null, bool? alsoSecondSlot = null, AssignTarget? storeIn = null, bool? includeNonReceivers = null, uint? heldCount = null, bool? allowCurrent = null, bool? visibleOnly = null, uint? components = null, float? radius = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null) =>
        Build(CommandId.RequestFocus2, [Bits(offsetX), Bits(offsetY), Bits(offsetZ), object1, object2, object3, object4, heldObject1, heldObject2, space, participant, (uint?)key, objectCount, Flag(nearest, false), Flag(useOffset, false), Flag(pickRandom, false), attachFilter, busyFilter, Flag(markBusy, false), Flag(alsoSecondSlot, false), (uint?)storeIn, Flag(includeNonReceivers, false), heldCount, Flag(allowCurrent, false), Flag(visibleOnly, false), components, Bits(radius)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12]);

    public static PS2BehaviourCommand SetSlotToPlayer() => Cmd0(CommandId.SetSlotToPlayer);

    public static PS2BehaviourCommand StorePlayerPosition() => Cmd0(CommandId.StorePlayerPosition);

    public static PS2BehaviourCommand ExitVehicleMode(uint player, uint? pad0 = null) =>
        Build(CommandId.ExitVehicleMode, [player], [pad0]);

    public static PS2BehaviourCommand SetVehicleRollerbrawl(uint driver, uint rider, uint? pad0 = null) =>
        Build(CommandId.SetVehicleRollerbrawl, [driver, rider], [pad0]);

    public static PS2BehaviourCommand SetVehicleHoverboard(uint on, uint? pad0 = null) =>
        Build(CommandId.SetVehicleHoverboard, [on], [pad0]);

    public static PS2BehaviourCommand SetupLookAt(float? rangeSq = null, float? rate = null, uint? pitchDown = null, uint? pitchUp = null, uint? yawMax = null, float? pitchUpSin = null, float? pitchDownSin = null, float? yawCos = null, uint? joint = null, uint? secondJoint = null, uint? referenceJoint = null, bool? wholeBody = null, bool? turnBody = null, bool? ignoreSounds = null, float? damping = null, uint? distanceGate = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null) =>
        Build(CommandId.SetupLookAt, [Bits(rangeSq), Bits(rate), pitchDown, pitchUp, yawMax, Bits(pitchUpSin), Bits(pitchDownSin), Bits(yawCos), joint, secondJoint, referenceJoint, Flag(wholeBody, false), Flag(turnBody, false), Flag(ignoreSounds, false), Bits(damping), distanceGate], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17]);

    public static PS2BehaviourCommand FocusNearNav(float distance, uint? code = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.FocusNearNav, [code, Bits(distance)], [pad0, pad1]);

    public static PS2BehaviourCommand FocusOnPlayer(uint player, uint? pad0 = null) =>
        Build(CommandId.FocusOnPlayer, [player], [pad0]);

    public static PS2BehaviourCommand BecomeSticky(uint? components = null, float? push = null, uint? stickFilter = null, uint? stickMessage = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null) =>
        Build(CommandId.BecomeSticky, [components, Bits(push), stickFilter, stickMessage], [pad0, pad1, pad2, pad3]);

    public static PS2BehaviourCommand PushStandingOn(float z, float? x = null, float? y = null, float? w = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null) =>
        Build(CommandId.PushStandingOn, [Bits(x), Bits(y), Bits(z), Bits(w)], [pad0, pad1, pad2, pad3, pad4]);

    public static PS2BehaviourCommand BecomeNormal(uint? mode = null, uint? pad0 = null) =>
        Build(CommandId.BecomeNormal, [mode], [pad0]);

    public static PS2BehaviourCommand AddSensor(SensorType? type = null, bool? includePlayer = null, float? weightDivisor = null, uint? components = null, uint? object1 = null, uint? object2 = null, uint? object3 = null, uint? object4 = null, uint? object5 = null, uint? object6 = null, uint? object7 = null, uint? object8 = null, uint? objectCount = null, float? radius = null, float? rangeSq = null, float? rate = null, float? interval = null, float? min = null, float? max = null, float? scale = null, float? facingWeight = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null) =>
        Build(CommandId.AddSensor, [(uint?)type, Flag(includePlayer, false), Bits(weightDivisor), components, object1, object2, object3, object4, object5, object6, object7, object8, objectCount, Bits(radius), Bits(rangeSq), Bits(rate), Bits(interval), Bits(min), Bits(max), Bits(scale), Bits(facingWeight)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15]);

    public static PS2BehaviourCommand DUMMY_584() => Cmd0(CommandId.DUMMY_584);

    public static PS2BehaviourCommand AddLives_Duplicate(int lives, uint? pad0 = null) =>
        Build(CommandId.AddLives_Duplicate, [Int(lives)], [pad0]);

    public static PS2BehaviourCommand DUMMY_586(uint value, uint? pad0 = null) =>
        Build(CommandId.DUMMY_586, [value], [pad0]);

    public static PS2BehaviourCommand TouchResponse(Toggle? motion10000 = null, Toggle? motion40000 = null, Toggle? motionE000 = null, uint? pad0 = null) =>
        Build(CommandId.TouchResponse, [(uint?)motion10000, (uint?)motion40000, (uint?)motionE000], [pad0]);

    public static PS2BehaviourCommand CutsceneStart(float seconds, uint? pad0 = null) =>
        Build(CommandId.CutsceneStart, [Bits(seconds)], [pad0]);

    public static PS2BehaviourCommand CutsceneEnd(float seconds, uint? pad0 = null) =>
        Build(CommandId.CutsceneEnd, [Bits(seconds)], [pad0]);

    public static PS2BehaviourCommand CutsceneShot(uint? yawPreset = null, uint? shotSize = null, uint? offCenter = null, uint? shot = null, uint? radiusFrom = null, bool? blendDistance = null, bool? holdEye = null, bool? reverseEase = null, uint? ease = null, bool? keepFov = null, float? pitch = null, float? distance = null, float? targetSeconds = null, float? eyeSeconds = null, float? height = null, float? yaw = null, bool? noAvoid = null, float? path1Position = null, float? path2Position = null, int? fov = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null) =>
        Build(CommandId.CutsceneShot, [yawPreset, shotSize, offCenter, shot, radiusFrom, Flag(blendDistance, false), Flag(holdEye, false), Flag(reverseEase, false), ease, Flag(keepFov, false), Bits(pitch), Bits(distance), Bits(targetSeconds), Bits(eyeSeconds), Bits(height), Bits(yaw), Flag(noAvoid, false), Bits(path1Position), Bits(path2Position), Int(fov)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11]);

    public static PS2BehaviourCommand ToggleCutsceneCamera(uint camera, bool? copyPlayer = null, bool? snap = null, uint? curve = null, float? seconds = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.ToggleCutsceneCamera, [camera, Flag(copyPlayer, false), Flag(snap, false), curve, Bits(seconds)], [pad0, pad1]);

    public static PS2BehaviourCommand CutsceneTargets(Designator target1, Designator? target2 = null, Designator? position1 = null, Designator? position2 = null, bool? mirror = null, bool? trackTargets = null, uint? path1 = null, uint? path2 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.CutsceneTargets, [(uint?)target1, (uint?)target2, (uint?)position1, (uint?)position2, Flag(mirror, false), Flag(trackTargets, false), path1, path2], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand StartWhackaworm(uint icon, ScriptValue seconds, ScriptValue target, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.StartWhackaworm, [icon, seconds.Word, target.Word], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand ProgressWhackaworm(int amount, uint? pad0 = null) =>
        Build(CommandId.ProgressWhackaworm, [Int(amount)], [pad0]);

    public static PS2BehaviourCommand EndWhackaworm() => Cmd0(CommandId.EndWhackaworm);

    public static PS2BehaviourCommand ReleaseCharacter() => Cmd0(CommandId.ReleaseCharacter);

    public static PS2BehaviourCommand WarpToPlayerChunk() => Cmd0(CommandId.WarpToPlayerChunk);

    public static PS2BehaviourCommand SetVehicleWrestleCreature() => Cmd0(CommandId.SetVehicleWrestleCreature);

    public static PS2BehaviourCommand FadeoutScreen(uint mode, float duration, bool? setColour = null, float? red = null, float? green = null, float? blue = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.FadeoutScreen, [mode, Flag(setColour, false), Bits(duration), Bits(red), Bits(green), Bits(blue)], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand DisplayBottomText(int text, float? x = null, float? y = null, float? red = null, float? green = null, float? blue = null, float? seconds = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null) =>
        Build(CommandId.DisplayBottomText, [Int(text), Bits(x), Bits(y), Bits(red), Bits(green), Bits(blue), Bits(seconds)], [pad0, pad1, pad2, pad3, pad4, pad5, pad6]);

    public static PS2BehaviourCommand ResetFall() => Cmd0(CommandId.ResetFall);

    public static PS2BehaviourCommand DismissCharacter(uint slot, uint? pad0 = null) =>
        Build(CommandId.DismissCharacter, [slot], [pad0]);

    public static PS2BehaviourCommand CameraFocusObject() => Cmd0(CommandId.CameraFocusObject);

    public static PS2BehaviourCommand CameraStopFocusObject() => Cmd0(CommandId.CameraStopFocusObject);

    public static PS2BehaviourCommand ClearBottomText() => Cmd0(CommandId.ClearBottomText);

    public static PS2BehaviourCommand SetCharacterHomeChunk() => Cmd0(CommandId.SetCharacterHomeChunk);

    public static PS2BehaviourCommand DisablePlayerControl() => Cmd0(CommandId.DisablePlayerControl);

    public static PS2BehaviourCommand SetNode120Flag(bool? keepMotionBlock = null, uint? pad0 = null) =>
        Build(CommandId.SetNode120Flag, [Flag(keepMotionBlock, false)], [pad0]);

    public static PS2BehaviourCommand SetPlayerFlag25(bool? clear = null, uint? pad0 = null) =>
        Build(CommandId.SetPlayerFlag25, [Flag(clear, false)], [pad0]);

    public static PS2BehaviourCommand PlaceCharacterInChunk(uint slot, uint? pad0 = null) =>
        Build(CommandId.PlaceCharacterInChunk, [slot], [pad0]);

    public static PS2BehaviourCommand HitInstancesInBoxes(uint mode, uint filter, uint? damage = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.HitInstancesInBoxes, [mode, damage, filter], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand ShowBottomText(float seconds, uint? pad0 = null) =>
        Build(CommandId.ShowBottomText, [Bits(seconds)], [pad0]);

    public static PS2BehaviourCommand HideBottomText(float seconds, uint? pad0 = null) =>
        Build(CommandId.HideBottomText, [Bits(seconds)], [pad0]);

    public static PS2BehaviourCommand FocusOnCamera() => Cmd0(CommandId.FocusOnCamera);

    public static PS2BehaviourCommand CharacterSoundProxy(uint mode, uint? pad0 = null) =>
        Build(CommandId.CharacterSoundProxy, [mode], [pad0]);

    public static PS2BehaviourCommand CameraNodeSetTarget() => Cmd0(CommandId.CameraNodeSetTarget);

    public static PS2BehaviourCommand SwitchCharacter(uint slot, uint? link = null, bool? useLink = null, Designator? target = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SwitchCharacter, [link, Flag(useLink, false), (uint?)target, slot], [pad0, pad1]);

    public static PS2BehaviourCommand CameraNodeEnableFlags() => Cmd0(CommandId.CameraNodeEnableFlags);

    public static PS2BehaviourCommand CameraNodeClearFlags() => Cmd0(CommandId.CameraNodeClearFlags);

    public static PS2BehaviourCommand SetCameraNodeValue(uint mode, uint? value = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetCameraNodeValue, [mode, value], [pad0, pad1]);

    public static PS2BehaviourCommand SetBodyMaskFlags(uint fields, uint? pad0 = null) =>
        Build(CommandId.SetBodyMaskFlags, [fields], [pad0]);

    public static PS2BehaviourCommand PushPlayerSkate(float speed, uint? pad0 = null) =>
        Build(CommandId.PushPlayerSkate, [Bits(speed)], [pad0]);

    public static PS2BehaviourCommand SetLinkedNearestPlayer(uint? firstLink = null, uint? lastLink = null, uint? pad0 = null) =>
        Build(CommandId.SetLinkedNearestPlayer, [firstLink, lastLink], [pad0]);

    public static PS2BehaviourCommand SetPlayerMode(uint mode, uint mode3, uint? mode2 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.SetPlayerMode, [mode, mode2, mode3], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand PlayMovie(uint movie, float? value = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.PlayMovie, [movie, Bits(value)], [pad0, pad1]);

    public static PS2BehaviourCommand AddAmmo(int amount, uint? pad0 = null) =>
        Build(CommandId.AddAmmo, [Int(amount)], [pad0]);

    public static PS2BehaviourCommand EnableBossMode(uint icon, ScriptValue health, float iconWidth, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null) =>
        Build(CommandId.EnableBossMode, [icon, health.Word, Bits(iconWidth)], [pad0, pad1, pad2]);

    public static PS2BehaviourCommand DamageBoss(int amount, uint? pad0 = null) =>
        Build(CommandId.DamageBoss, [Int(amount)], [pad0]);

    public static PS2BehaviourCommand ExitBossMode() => Cmd0(CommandId.ExitBossMode);

    public static PS2BehaviourCommand FinalBossSetupWeapons(Designator? target = null, uint? operation = null, uint? mask = null, float? rate1 = null, float? rate2 = null, uint? joints = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.FinalBossSetupWeapons, [(uint?)target, operation, mask, Bits(rate1), Bits(rate2), joints], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand FinalBossActivateWeapons(Designator? target = null, uint? operation = null, uint? mask = null, float? rate1 = null, float? rate2 = null, uint? joints = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.FinalBossActivateWeapons, [(uint?)target, operation, mask, Bits(rate1), Bits(rate2), joints], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand FinalBossReleaseWeapons(Designator? target = null, uint? operation = null, uint? mask = null, float? rate1 = null, float? rate2 = null, uint? joints = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.FinalBossReleaseWeapons, [(uint?)target, operation, mask, Bits(rate1), Bits(rate2), joints], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand FinalBossAimWeapons(Designator? target = null, uint? operation = null, uint? mask = null, float? rate1 = null, float? rate2 = null, uint? joints = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.FinalBossAimWeapons, [(uint?)target, operation, mask, Bits(rate1), Bits(rate2), joints], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand AddComponent(uint component, bool? onlyIfNone = null, uint? pad0 = null) =>
        Build(CommandId.AddComponent, [component, Flag(onlyIfNone, false)], [pad0]);

    public static PS2BehaviourCommand SetGlobalProgression2(int? value = null, ScriptValue? fromValue = null, uint? pad0 = null, uint? pad1 = null) =>
        Build(CommandId.SetGlobalProgression2, [Int(value), fromValue?.Word], [pad0, pad1]);

    public static PS2BehaviourCommand SuspendPlayerVehicle(uint slot, uint? pad0 = null) =>
        Build(CommandId.SuspendPlayerVehicle, [slot], [pad0]);

    public static PS2BehaviourCommand ResumePlayerVehicle(uint slot, uint? pad0 = null) =>
        Build(CommandId.ResumePlayerVehicle, [slot], [pad0]);

    public static PS2BehaviourCommand CameraTopdownMode() => Cmd0(CommandId.CameraTopdownMode);

    public static PS2BehaviourCommand FinalBossWeaponRates(Designator? target = null, uint? operation = null, uint? mask = null, float? rate1 = null, float? rate2 = null, uint? joints = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.FinalBossWeaponRates, [(uint?)target, operation, mask, Bits(rate1), Bits(rate2), joints], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand PlayCredits() => Cmd0(CommandId.PlayCredits);

    public static PS2BehaviourCommand SetComponentEmitters(uint e0, uint e1, uint e6, uint? e2 = null, uint? e3 = null, uint? e4 = null, uint? e5 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null) =>
        Build(CommandId.SetComponentEmitters, [e0, e1, e2, e3, e4, e5, e6], [pad0, pad1, pad2, pad3, pad4, pad5, pad6]);

    public static PS2BehaviourCommand DisplayBottomTextInstance(float? x = null, float? y = null, float? red = null, float? green = null, float? blue = null, float? seconds = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null) =>
        Build(CommandId.DisplayBottomTextInstance, [Bits(x), Bits(y), Bits(red), Bits(green), Bits(blue), Bits(seconds)], [pad0, pad1, pad2, pad3, pad4, pad5]);

    public static PS2BehaviourCommand SetSpline(ScriptValue a0, ScriptValue a1, ScriptValue a2, ScriptValue a3, ScriptValue a4, ScriptValue a5, ScriptValue a6, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null) =>
        Build(CommandId.SetSpline, [a0.Word, a1.Word, a2.Word, a3.Word, a4.Word, a5.Word, a6.Word], [pad0, pad1, pad2, pad3, pad4, pad5, pad6]);

    public static PS2BehaviourCommand CharactersToIdle(bool? all = null, bool? player0 = null, bool? player1 = null, bool? player5 = null, uint? pad0 = null) =>
        Build(CommandId.CharactersToIdle, [Flag(all, false), Flag(player0, false), Flag(player1, false), Flag(player5, false)], [pad0]);

    public static PS2BehaviourCommand Revive(uint? pad0 = null) =>
        Build(CommandId.Revive, [], [pad0]);

    public static PS2BehaviourCommand SetSkateIds(uint? trailCount = null, uint? soundCount = null, bool? append = null, uint? trail0 = null, uint? trail1 = null, uint? trail2 = null, uint? trail3 = null, uint? trail4 = null, uint? trail5 = null, uint? trail6 = null, uint? trail7 = null, uint? trail8 = null, uint? trail9 = null, uint? trail10 = null, uint? trail11 = null, uint? sound0 = null, uint? sound1 = null, uint? sound2 = null, uint? sound3 = null, uint? sound4 = null, uint? sound5 = null, uint? sound6 = null, uint? sound7 = null, uint? sound8 = null, uint? sound9 = null, uint? sound10 = null, uint? sound11 = null, uint? sound12 = null, uint? sound13 = null, uint? sound14 = null, uint? sound15 = null, uint? sound16 = null, uint? sound17 = null, uint? sound18 = null, uint? sound19 = null, uint? sound20 = null, uint? sound21 = null, uint? sound22 = null, uint? sound23 = null, uint? sound24 = null, uint? sound25 = null, uint? sound26 = null, uint? sound27 = null, uint? sound28 = null, uint? sound29 = null, uint? sound30 = null, uint? pad0 = null, uint? pad1 = null, uint? pad2 = null, uint? pad3 = null, uint? pad4 = null, uint? pad5 = null, uint? pad6 = null, uint? pad7 = null, uint? pad8 = null, uint? pad9 = null, uint? pad10 = null, uint? pad11 = null, uint? pad12 = null, uint? pad13 = null, uint? pad14 = null, uint? pad15 = null, uint? pad16 = null, uint? pad17 = null, uint? pad18 = null, uint? pad19 = null, uint? pad20 = null, uint? pad21 = null, uint? pad22 = null) =>
        Build(CommandId.SetSkateIds, [trailCount, soundCount, Flag(append, false), trail0, trail1, trail2, trail3, trail4, trail5, trail6, trail7, trail8, trail9, trail10, trail11, sound0, sound1, sound2, sound3, sound4, sound5, sound6, sound7, sound8, sound9, sound10, sound11, sound12, sound13, sound14, sound15, sound16, sound17, sound18, sound19, sound20, sound21, sound22, sound23, sound24, sound25, sound26, sound27, sound28, sound29, sound30], [pad0, pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22]);

    private static PS2BehaviourCommand Build(CommandId id, object?[] values, uint?[] pads)
    {
        var c = new PS2BehaviourCommand { CommandIndex = (ushort)id };
        c.Arguments.AddRange(ReadableScript.EncodeValues(ReadableScript.ByOp[(ushort)id], values, pads));
        return CompiledBehaviour.Recorded(c);
    }

    private static PS2BehaviourCommand Cmd0(CommandId id) => CompiledBehaviour.Recorded(new() { CommandIndex = (ushort)id });
    private static object? Bits(float? f) => f is float x ? BitConverter.SingleToUInt32Bits(x) : null;
    private static object? Flag(bool? b, bool inverted) => b is bool x ? (x != inverted ? 1u : 0u) : null;
    private static object? Int(int? v) => v is int x ? unchecked((uint)x) : null;
}
