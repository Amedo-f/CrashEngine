using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabAgent
{
    private string? _textStyleKey; // Amedo 2026-10-01
    private string TextStyleKey => _textStyleKey ??= Screen.TextStyles.ObjectKey(Chunk.Name, Instance.Source.GetID());

    private void Execute(PS2BehaviourCommand cmd, Level ctx)
    {
        var a = cmd.Arguments;
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i)); // Amedo 2026-09-30
        switch (cmd.CommandIndex)
        {
            case 8:
            case 112: SpawnResidentAgent(cmd); return;
            case 111: CacheLinkedInstance(); return;
            case 47:
            case 64:
            case 94: AttachLinkedObject(cmd); return;
            case 48: DropAttachedObject(Arg(0)); return; // Amedo 2026-09-28
            case 574: SetupLookAt_23DC08(a); return; // Amedo 2026-09-28
            case 582: AddSensor_23D9E8(a); return; // Amedo 2026-10-05
            case 136: SetSensor_223CB0(a); return; // Amedo 2026-10-05
            case 139: SetInfluence_223DB0(a); return; // Amedo 2026-10-05
            case 118: SetLookTarget_21C930(Arg(0), Arg(1)); return; // Amedo 2026-09-28
            case 78: SetObject(Arg(0), Arg(1)); return;
            case 79: Keep_20FE50(Arg(0)); return; // Amedo 2026-09-30
            case 9:
            case 543: DoAnimation(Arg(0), a); return;
            case 56: ClearAnimation_221558(Arg(0), Arg(1)); return; // Amedo
            case 14: NowMoveForwards(Arg(0)); return;
            case 3:
            case 105: PositionWarp(a); return;
            case 177:
            case 179: WarpAgent_216B70(a); return; // Amedo 2026-09-29
            case 524: SetKeyNearestPlayer(a); return;
            case 36: PreviousRouteStep_20EEB0(); return; // Amedo
            case 151: NextRouteStep_20EF40(); return; // Amedo
            case 213: RestartRoute_20EF20(); return; // Amedo
            case 39: ReleaseRoute_20EFC8(); return; // Amedo
            case 40:
                if ((Arg(0) & 1) != 0) // Amedo 2026-09-27
                {
                    var (bmin, bmax) = WorldBox();
                    var ext = bmax - bmin;
                    _logicalRadius = MathF.Max(MathF.Max(ext.X, ext.Y), ext.Z) * 0.5f;
                }
                else _logicalRadius = BitConverter.UInt32BitsToSingle(Arg(1));
                if (_physics?.Body is { IsBox: false } sphere) { sphere.IsEllipsoid = false; sphere.Radius = _logicalRadius; }
                return;
            case 549: GetShortRoute(a); return;
            case 28:
            case 103: SetFocusToKey(Arg(0)); return;
            case 45:
            case 90:
            case 91:
            case 104: AssignAgent(AgentRef((int)(Arg(0) & 0xFF)), Arg(0) >> 8); return;
            case 52: _hasFocusObject = _hasFocusPosition = false; return;
            case 60:
            case 108: SetFocusPosition(a); return;
            case 567: if (World.Player is { } focusPlayer) Store(focusPlayer.GamePosition); return; // Amedo
            case 214: World.Rumble_27B4F0?.Invoke(BitConverter.UInt32BitsToSingle(Arg(0))); return; // Amedo
            case 187: case 188: case 189: case 190: case 191: ShadowCommand_224210(cmd.CommandIndex, a); return; // Amedo
            case 204: SetFocusPositionOffset_2150C0(a); return; // Amedo
            case 205: SetFocusPositionAtAngle_2154D8(Arg(0), Arg(1)); return; // Amedo
            case 62:
            case 109: AddNoiseToFocus(cmd); return;
            case 59: // Amedo
            case 106: CopyFromMessenger_2144F0(Arg(0)); return; // Amedo 2026-10-05
            case 170: SetFocusCounter_212AA8(Arg(0), Arg(1), Arg(2)); return; // Amedo 2026-10-05
            case 162: ModifyFocusCounter_212870(Arg(0), Arg(1)); return; // Amedo
            case 145: if (ctx.Depth > 0) Levels[ctx.Depth - 1].Value_1A = (ushort)Arg(0); return; // Amedo
            case 119: ClearImpact_2530D0(); return; // Amedo
            case 121: FocusBesidePlayer_213970(F(0)); return; // Amedo
            case 150: StrafeTowardsTarget_218278(a); return; // Amedo
            case 554: DetachAll_1292D0(); return; // Amedo 2026-10-05
            case 67:
            case 101:
            case 102:
                if (_messageSource is { } source) AssignAgent(source, Arg(0));
                else ClearAssignment_23DF90((int)(Arg(0) & 7)); // Amedo
                return;
            case 556:
                if (World.Player is { } player) FocusOn(player);
                return;
            case 557:
            case 564:
            case 565: RequestFocus(a); return;
            case 42: { int p = (int)(Arg(0) & 0xFF); Priority = p < 101 ? p & 0x7F : -1; return; }
            case 31: Restartable = (Arg(0) & 1) != 0; return; // Amedo 2026-09-27
            case 34: World.SetZoneState(this, (Arg(0) & 0xFF) != 0); return; // Amedo 2026-09-28
            case 35: World.ToggleZoneState(this); return;
            case 521: SetPlayerInput(Arg(0), Arg(1)); return; // Amedo 2026-09-27
            case 645: AddComponent(Arg(0)); return; // Amedo 2026-09-27
            case 641: // Amedo
            case 642:
            case 643:
            case 644:
            case 653: FinalBossCommand_116B08(a); return;
            case 655: SetComponentEmitters(a); return; // Amedo 2026-09-27
            case 54:
            case 65: SendMessage(a); return;
            case 55: BroadcastUserMessage_250550(a); return; // Amedo 2026-09-29
            case 534: SetGlobalProgression_1203B8(Arg(0), Arg(1)); return;
            case 576:
                if ((Arg(0) & 0xFF) != 6 && World.PlayerSlot_570((int)(Arg(0) & 0xFF)) is { } slotPlayer) FocusOn(slotPlayer);
                return;
            case 647: SetGlobalProgression2_120590(Arg(0), Arg(1)); return;
            case 63: ctx.Stopped = true; PacketEnded_23D8B0(); _pendingDefault = true; return; // Amedo
            case 531: ctx.Stopped = true; _pendingDefault = true; return;
            case 66: Message = NoMessage; _messageSource = null; MessageTime = double.NegativeInfinity; return; // Amedo 2026-09-28
            case 68: SetCounter(Arg(0), Arg(1)); return;
            case 69: ModifyCounter(Arg(0), Arg(1), Arg(2)); return; // Amedo
            case 163: UnlinkFromTarget_210DB8(Arg(0)); return; // Amedo
            case 164: AddToLinkedObjectsByte_218FD8(Arg(0), Arg(1), Arg(2)); return; // Amedo
            case 85:
                if ((Arg(0) & 7) < 3) DestroySelf(); // Amedo 2026-10-01
                return;
            case 86: // Amedo 2026-10-05
                if (Chunk is { } scene)
                    scene.Sounds[Arg(6) & 1] = new SceneSound((byte)Arg(0), (Arg(0) & 0x100) != 0, (Arg(0) & 0x200) != 0,
                                                              BitConverter.UInt32BitsToSingle(Arg(1)), BitConverter.UInt32BitsToSingle(Arg(2)), BitConverter.UInt32BitsToSingle(Arg(3)));
                return;
            case 75: DestroySpawnedAttachment_21FD48(Arg(0)); return; // Amedo 2026-10-01
            case 122: CopyTarget(Arg(0)); return;
            case 146: // Amedo 2026-09-27
            case 154:
            case 155: AssignFromLinks(Arg(0)); return;
            case 4: SetKey(Arg(0)); return;
            case 5: (_controller ?? this).NextKey(); return;
            case 147: PrevKey(); return;
            case 27: StoreSpace(Arg(0)); return;
            case 29: RotationWarp(a); return;
            case 178: RotateAgent_217A38(a); return; // Amedo
            case 617: HitInstancesInBoxes_10EE60(a); return; // Amedo
            case 61: StopNodeMotion_23DF48(); return; // Amedo
            case 96: Launch_24E358(a); return; // Amedo 2026-09-30
            case 193: LaunchSelf_244B60(a); return; // Amedo 2026-09-30
            case 72: ColliderLaunchNow_244538(a); return; // Amedo 2026-09-30
            case 133: ClearDamage_220B18(); return; // Amedo 2026-09-30
            case 95: ReleaseCached_24D858(Arg(0)); return; // Amedo
            case 23: LinkReferenced_24C270(Arg(0)); return; // Amedo
            case 658: SetSpline_1196A8(a); return; // Amedo
            case 97: _slot118 = null; return;
            case 98: _slot11C = null; return;
            case 134: if (Requester is { } focusRequester) FocusOn(focusRequester); return; // Amedo 2026-10-06
            case 57: RequestAttachmentFocus_221D68(Arg(0)); return; // Amedo 2026-09-30
            case 211: _timer5C = World.NowTicks; return; // Amedo 2026-09-30
            case 212: _timer5C = 0; return;
            case 158: SetTouchMessage_253498(a); return; // Amedo 2026-09-30
            case 159: ClearTouchMessage_2534F8(); return;
            case 206: _lockDepth = ActiveDepth; return; // Amedo 2026-09-30
            case 207: _lockDepth = 0xFF; return;
            case 566: if (World.Player is { } thrower) _slot118 = thrower; return; // Amedo 2026-09-30
            case 110: _hasStoredPosition = false; return;
            case 173: _pathIndex = (int)(Arg(0) & 0xFF); return;
            case 171: RequestOnTarget(Arg(0), Arg(1)); return; // Amedo 2026-09-28
            case 172: ReleaseTarget(Arg(0)); return;
            case 185: PrepareSpeech(Arg(0), Arg(1)); return;
            case 195: StopSpeech_224B38(); return; // Amedo
            case 634: PlayMovie_121028(Arg(0), Arg(1)); return; // Amedo
            case 603: World.DisplayBottomText_120F28((int)Arg(0), new System.Numerics.Vector2(F(1), F(2)), new System.Numerics.Vector3(F(3), F(4), F(5)), F(6), TextStyleKey); return; // Amedo 2026-10-01
            case 657: World.DisplayBottomTextInstance_112A20((uint)Var(0), new System.Numerics.Vector2(F(0), F(1)), new System.Numerics.Vector3(F(2), F(3), F(4)), F(5), TextStyleKey); return;
            case 608: World.ClearBottomText_120FF8(); return;
            case 619:
                World.ShowBottomText_176C90(Screen.GameScreen.Ticks(F(0))); // Amedo 2026-10-01
                return;
            case 620: World.HideBottomText_17BA78(Screen.GameScreen.Ticks(F(0))); return;
            case 589: World.StartCutscene_176A48(BitConverter.UInt32BitsToSingle(Arg(0)), this); return;
            case 590: World.EndCutscene_176B98(BitConverter.UInt32BitsToSingle(Arg(0)), this); return;
            case 591:
            case 594:
            case 595: // Amedo 2026-09-29
            case 626: // Amedo
            case 627:
            case 628:
            case 560:
            case 559: // Amedo
            case 652: CutsceneCamera(cmd.CommandIndex, a); return; // Amedo
            case 606: CameraFocusObject_10F2D8(this); return; // Amedo
            case 607: CameraFocusObject_10F2D8(null); return;
            case 623: World.CutsceneCamera?.CameraNodeSetTarget_120110(this); return; // Amedo
            case 174: SetFocusToOwner_221E68(); return; // Amedo
            case 625: SwitchCharacter_11B700(Arg(0), Arg(1)); return; // Amedo
            case 659: CharactersToIdle(Arg(0)); return;
            case 186: PlaySpeech(); return;
            case 183: SetController(Arg(0), true); return;
            case 184: SetController(Arg(0), false); return;
            case 124:
                if (RootController().Slot((int)(Arg(0) & 0xFFFF)) is { Graph: { } g } r) // Amedo
                {
                    var entered = EnterChannel((int)((Arg(0) >> 16) & 1)); // Amedo 2026-10-05
                    _pendingBehaviour = (g, r.Starter, this, null);
                    LeaveChannel(entered);
                }
                return;
            case 149: SpinWithParent_24EE10(Arg(0), Arg(1)); return; // Amedo 2026-10-01
            case 126: NextLink_222938(); return; // Amedo 2026-10-01
            case 132: SetLinkCursor_222BC0(Arg(0)); return;
            case 523: ApplyVelocity(a); return; // Amedo 2026-09-27
            case 533: NowGoBackCollidable_11F270(Arg(0)); return; // Amedo
            case 512: CreateCrateContents(a); return; // Amedo 2026-09-27
            case 513: World.PickUpWumpa(this, (int)Arg(0)); return;
            case 518: SetCrate_122EE8(Arg(1)); return; // Amedo
            case 621: FocusOnCamera_120068(); return; // Amedo
            case 602: World.FadeoutScreen_1124D0(Arg(0), F(1), F(3), F(4), F(5)); return; // Amedo
            case 527: return; // Amedo
            case 584: return; // Amedo
            case 520: World.PickUpHealth_11A270(this); return; // Amedo 2026-10-04
            case 535: World.AddCrystal_122370(); return; // Amedo 2026-10-01
            case 537: World.AddGem_11A188((int)Arg(0)); return; // Amedo 2026-10-01
            case 638: World.EnableBossMode_176D40(BitConverter.UInt32BitsToSingle(Arg(2)), ValueOf(Arg(1)), IconOgi_261548(Arg(0))); return; // Amedo 2026-10-01
            case 639: World.DamageBoss_1796B8((int)Arg(0)); return; // Amedo 2026-10-01
            case 636: World.PlayerCharacter?.Gun?.AddAmmo_15F0A0((int)Arg(0)); return; // Amedo
            case 596: World.StartWhackaworm_176E30(GameMath.Trunc(FloatValueOf(Arg(1)) * 9000f), ValueOf(Arg(2)), IconOgi_261548(Arg(0))); return; // Amedo 2026-10-01
            case 597: World.ProgressWhackaworm_179748((int)Arg(0)); return; // Amedo 2026-10-01
            case 598: World.EndWhackaworm_17BB50(); return; // Amedo 2026-10-01
            case 640: World.ExitBossMode_17BAD0(); return; // Amedo 2026-10-01
            case 10:
            case 544: DoParticle(a); return;
            case 11:
            case 545: DoSound(a); return; // Amedo 2026-09-28
            case 615:
                if (World.PlayerCharacter is { } resetLock)
                    resetLock.MotionFlagsHigh = (Arg(0) & 1) != 0 ? resetLock.MotionFlagsHigh & ~(1u << 25) : resetLock.MotionFlagsHigh | (1u << 25);
                return;
            case 516: // Amedo
            {
                bool level = ((Arg(0) >> 8) & 0xFF) != 0;
                if (World.SetRespawn(this, level) && level) World.Autosave_176F28((ushort)Instance.Source.GetID());
                return;
            }
            case 517: // Amedo
                World.Log?.Invoke($"[AgentLab {World.Now:0.00}s] ResetGame by {Entity.Name} in '{Behaviour?.Name ?? "-"}'");
                World.RestartFromCheckpoint_17B6C0();
                return;
            case 92:
            case 93:
            case 585: World.AddLives((int)Arg(0), this); return;
            case 561: LinkToFocusCharacter_11AD48(Arg(0)); return; // Amedo 2026-10-05
            case 562: OwnCharacter?.UnlinkCharacters_17B5C0(); return; // Amedo 2026-10-05
            case 74: ForceAnimationUpdate_2215F0(Arg(0)); return; // Amedo 2026-10-05
            case 558: SetFocusProperties_1142E8(Arg(0)); return; // Amedo 2026-10-05
            case 570: SetVehicleRollerbrawl_1247E8(Arg(0)); return; // Amedo 2026-09-29
            case 571: SetVehicleHoverboard_124DE0(Arg(0)); return; // Amedo
            case 555: SetVehicleHumiliskate_124B68(a); return; // Amedo
            case 661: SetSkateIds_119F50(a); return; // Amedo
            case 622: CharacterSoundProxy_10E048(Arg(0)); return; // Amedo
            case 633: SetPlayerMode_120288(Arg(0), Arg(1), Arg(2)); return;
            case 569: ExitVehicleMode_1298E0(Arg(0)); return;
            case 116: DeleteLookAt_23DAE0(); return; // Amedo 2026-10-02
            case 114: _lookAt?.Disable_23F640(); return;
            case 115: _lookAt?.Enable_237530(); return;
            case 587: TouchResponse_10D060(Arg(0)); return; // Amedo 2026-10-01
            case 575: FocusNearNav_113150(Arg(0)); return; // Amedo 2026-10-05
            case 169: PointToward_214CD0(Arg(0), F(1)); return;
            case 196: StopStoredSound_23DB30(); return;
            case 215: ModifyStoredSound_224CE8(Arg(0), F(1), F(2)); return; // Amedo
            case 599: ReleaseCharacter_122610(); return;
            case 605: DismissCharacter_1229F8(Arg(0)); return; // Amedo 2026-10-06
            case 616: PlaceCharacterInChunk_122AF8(Arg(0)); return; // Amedo
            case 611: SetCharacterHomeChunk_122B68(); return;
            case 613: World.DisablePlayerControl_17C190(); return; // Amedo
            case 127: if (_physics is { } gravityCtrl) SetRigidBodyGravity_253988(gravityCtrl, F(0)); return; // Amedo
            case 129: ActivatePhysics_2461C0(); return; // Amedo
            case 630: SetBodyMaskFlags_10D2C8(Arg(0)); return; // Amedo
            case 600: WarpToPlayerChunk_11A928(); return;
            case 601: WrestleCreature_124950(); return; // Amedo
            case 660: Revive_122D08(); return;
            case 194: SetContactSounds_224BA8(Arg(0), Arg(1)); return;
            case 514: // Amedo 2026-09-28
            case 546:
            case 563: CreateDamage(a); return;
            case 33: TriggerLinkedObjects(Arg(0)); return;
            case 77: AgentFlags &= ~0x40u; return;
            case 519: TriggerBalancedCrateFalling(); return;
            case 166: TouchAround_22E888(FloatValueOf(Arg(0))); return; // Amedo 2026-10-05
            case 76: ApplyImpulse_244FC8(a); return; // Amedo 2026-10-06
            case 580: PushStandingOn_123668(a); return; // Amedo 2026-10-06
            case 49: ThrowAttachedObject_24DD10(a); return; // Amedo 2026-10-06
            case 80: AttachSpring_24CE40(a); return; // Amedo
            case 81: DetachAllSprings_1969B0(); return; // Amedo
            case 522: TriggerAllNitroCrates(); return;
            case 117: MakeNoise_2549E0(BitConverter.UInt32BitsToSingle(Arg(0)), BitConverter.UInt32BitsToSingle(Arg(1))); return; // Amedo
            case 156: SetNode150Fields_254B48(Arg(0)); return;
            case 167: SetSurface_253058(Arg(0)); return; // Amedo
            case 528: ReduceHitPoints((int)Arg(0)); return; // Amedo 2026-09-28
            case 529: SetHitPoints((int)Arg(0)); return;
            case 88: BeginMusic(a); return;
            case 89: EndContextMusic(a); return;
            case 176: EndMusic_224A10(a); return; // Amedo 2026-09-29
            case 165: ForceVolumeController_24F340(a); return; // Amedo 2026-09-29
            case 548: Shoot_123F98(a); return; // Amedo 2026-10-06
            case 1:
            case 541: AddTrail(a); return;
            case 2:
            case 542: ClearTrails(); return;
            case 44: SetCollisions(a); return; // Amedo 2026-09-27
            case 53: ReleaseRigidBody_23DED8(); return; // Amedo
            case 12: SetWobble(a); return;
            case 13: ClearWobble(); return;
            case 87: AlterWobblePhase_21C320(a); return; // Amedo
            case 148: SetWobbleAmplitude_223AF8(a); return; // Amedo
            case 631: PushPlayerSkate_11B830(F(0)); return; // Amedo
            case 650: SuspendPlayerVehicle_1299C8((int)Arg(0), true); return; // Amedo
            case 651: SuspendPlayerVehicle_1299C8((int)Arg(0), false); return; // Amedo
            case 654: World.RequestCredits_17B7C8(); return; // Amedo
            case 168: PushBodiesInSphere_2457F0(a); return; // Amedo
            case 202: DestroyInstanceInBox_24F4B0(); return; // Amedo
            case 604: OwnCharacter?.ResetFall_13F820(); return; // Amedo
            case 586: return; // Amedo
            case 632: SetLinkedNearestPlayer_1250E0(Arg(0)); return; // Amedo
            case 198: SetCollisionBoxSize_221108(F(0), F(1), F(2)); return; // Amedo
            case 130: SetPhysicsSizes_253380(F(0), F(1), Arg(2), Arg(3)); return; // Amedo
            case 131: MagnetPullToFocus_245340(); return; // Amedo
            case 123: LinkToNearestPoint_219BE8(); return; // Amedo
            case 82:
            case 83:
                _contactResponse = a;
                AgentFlags |= 0x8000; // Amedo 2026-09-30
                return;
            case 577: BecomeSticky_11F440(a); return; // Amedo
            case 581: BecomeNormal_11F4F8(Arg(0)); return;
            case 614: SetNode120Flag_11F610(Arg(0)); return;
            case 84: ReleaseMotionBlock_23DDD0(); return; // Amedo
            case 120:
                if ((AgentFlags & 0x8000) == 0) return; // Amedo 2026-10-06
                ActivateResponse_23D648();
                _keep88 |= 0x200; // Amedo 2026-10-06
                return;
            case 525: RaycastFocusPosition(a); return;
            case 125:
                if (_hasFocusPosition)
                    FocusAt(_focusPosition + new System.Numerics.Vector3(BitConverter.UInt32BitsToSingle(Arg(1)), BitConverter.UInt32BitsToSingle(Arg(2)), BitConverter.UInt32BitsToSingle(Arg(3))));
                return;
            case 515:
            {
                uint mask = Arg(0) & 0xFFFF, values = Arg(0) >> 16;
                Trace($"SetAgent mask 0x{mask:X} values 0x{values:X}"); // Amedo
                if ((mask & 1) != 0) // Amedo
                {
                    if ((values & 1) != 0) Wake_1974C8();
                    else
                    {
                        ReleaseAttachments();
                        Sleep_197508();
                    }
                }
                if ((mask & 2) != 0)
                {
                    AgentFlags = (values & 2) != 0 ? AgentFlags | 0x400 : AgentFlags & ~0x400u;
                    ApplyRendered(); // Amedo 2026-09-27
                }
                if ((mask & 4) != 0) AgentFlags = (values & 4) != 0 ? AgentFlags | 0x10 : AgentFlags & ~0x10u;
                if ((mask & 8) != 0) AgentFlags = (values & 8) != 0 ? AgentFlags | 0x8 : AgentFlags & ~0x8u; // Amedo 2026-09-27
                if ((mask & 0x10) != 0) AgentFlags = (values & 0x10) != 0 ? AgentFlags | 0x1000 : AgentFlags & ~0x1000u;
                if ((mask & 0x20) != 0 && ControllerType == 15) _motion14 = (values & 0x20) != 0 ? _motion14 | 1u : _motion14 & ~1u; // Amedo
                if (ControllerType >= 0)
                {
                    var own = OwnCharacter; // Amedo 2026-09-29
                    uint m10 = own is not null ? (uint)own.MotionWord : Motion10;
                    if ((mask & 0x40) != 0) m10 = (m10 & ~0x100u) | ((values & 0x40) != 0 ? 0x100u : 0u);
                    if ((mask & 0x80) != 0) m10 = (m10 & ~0x400u) | ((values & 0x80) != 0 ? 0u : 0x400u);
                    if ((mask & 0x100) != 0) m10 = (m10 & ~0x1000u) | ((values & 0x100) != 0 ? 0x1000u : 0u);
                    if ((mask & 0x200) != 0) m10 = (m10 & ~0x200u) | ((values & 0x200) != 0 ? 0x200u : 0u);
                    if (own is not null) own.MotionWord = (int)m10;
                    else Motion10 = m10;
                }
                return;
            }
        }
        World.Unimplemented("command", cmd.CommandIndex, AgentLabWorld.CommandName(cmd.CommandIndex));
    }

    // Amedo 2026-10-01
    public ushort IconOgi_261548(uint slot) =>
        slot != 0xFFFFFFFF && RootController().GameObject is { } obj && slot < obj.OGISlots.Count ? obj.OGISlots[(int)slot] : (ushort)0xFFFF;

    private int ValueOf(uint word) =>
        (word & 6) != 0 ? 0 : (word & 1) != 0 ? Var((int)(word >> 3)) : (int)word >> 3;

    private float FloatValueOf(uint word) =>
        ((word >> 1) & 3) != 2 ? 0f
        : (word & 1) != 0 ? FloatVar((int)(word >> 3))
        : BitConverter.Int32BitsToSingle((int)(word & ~7u));

    // Amedo
    private int AngleValue_209320(uint word)
    {
        if (((word >> 1) & 3) != 1) return GameMath.FromValue_11BFA0(0f, 0);
        if ((word & 1) == 0) return GameMath.FromValue_11BFA0(BitConverter.UInt32BitsToSingle(word & ~7u), 0);
        return AngleVariable_261778((int)(word >> 3));
    }

    // Amedo
    private int AngleVariable_261778(int index)
    {
        var own = Instance.Source.ParamList1;
        int count = own?.Count ?? 0;
        if (index < count) return (int)own![index];
        var defaults = GameObject?.InstFlags;
        int k = index - count;
        return GameMath.FromValue_11BFA0(defaults is not null && k < defaults.Count ? BitConverter.UInt32BitsToSingle(defaults[k]) : 0f, 0);
    }

    // Amedo
    private void ClearAnimation_221558(uint joint, uint time)
    {
        if (Anim is null) return;
        float blend = FloatValueOf(time);
        if ((joint & 0xFF) != 0xFF) return;
        Trace($"ClearAnimation blend {blend:0.###}");
        Anim.RequestNone(blend);
    }

    private void DoAnimation(uint arg0, List<uint> args)
    {
        if (args.Count < 6) return;
        SetShadowSlot_211B08(arg0); // Amedo
        int count = (int)(arg0 & 0xF);
        int pick = count == 1 ? 0 : World.Rng.GetRandom_18C018(count); // Amedo
        int slot = (int)((args[5 + pick / 4] >> (8 * (pick % 4))) & 0xFF);
        float blend = FloatValueOf(args[1]); // Amedo 2026-09-27
        if (RootController() is { } root && !ReferenceEquals(root, this)) // Amedo 2026-09-28
        {
            if (ForeignSlot(root, slot, blend) is not { } foreign) return;
            if (foreign.Anim is null)
            {
                Trace($"slot {slot} from {root.Entity.Name}: model only (OGI {foreign.Ogi.GetID():X})");
                Anim!.ShowModel(foreign, blend);
                if (!Rendered) ApplyRendered();
                return;
            }
            Trace($"slot {slot} from {root.Entity.Name}: anim 0x{foreign.AnimId:X} on OGI {foreign.Ogi.GetID():X}");
            PlayGroup(foreign, arg0, args, blend);
            return;
        }
        if (PlayModernSlot(slot, arg0, args, blend)) return; // Amedo 2026-11-18
        if (Anim is null) return;
        if (GameObject is { } own && (slot >= own.OGISlots.Count || own.OGISlots[slot] == 0xFFFF)) return; // Amedo
        var group = Anim.Groups.FirstOrDefault(g => g.SlotIndex == slot);
        if (group is null)
        {
            if (Anim.ModelGroups.FirstOrDefault(g => g.SlotIndex == slot) is { } model) // Amedo 2026-09-28
            {
                Trace($"slot {slot}: model only (OGI {model.Ogi.GetID():X})");
                Anim.ShowModel(model, blend);
                if (!Rendered) ApplyRendered();
            }
            else if (GameObject is { } go && (slot >= go.AnimationSlots.Count || go.AnimationSlots[slot] == 0xFFFF)) Anim.RequestNone(blend);
            return;
        }
        PlayGroup(group, arg0, args, blend);
    }

    // Amedo 2026-09-28
    private void PlayGroup(AnimatedObject.SlotGroup group, uint arg0, List<uint> args, float blend)
    {
        if (Anim is null) return;
        float speed = RequestSpeed(arg0, args, AnimatedObject.NaturalDuration(group)); // Amedo 2026-11-18
        Anim.Request(group, arg0, blend, speed, BitConverter.UInt32BitsToSingle(args[4])); // Amedo 2026-09-27
        if (!Rendered) ApplyRendered(); // Amedo 2026-09-27
    }

    // Amedo 2026-11-18
    private float RequestSpeed(uint arg0, List<uint> args, float naturalDuration)
    {
        float speed = 1f;
        if ((arg0 & 0x4000) != 0)
        {
            speed = FloatValueOf(args[2]);
            if ((arg0 & 0x10000) != 0) speed += World.Rng.GetRandFloat__18C130(FloatValueOf(args[3])); // Amedo
        }
        else if ((arg0 & 0x8000) != 0)
        {
            float wanted = FloatValueOf(args[2]);
            speed = wanted > 0f ? naturalDuration / wanted : 1e30f;
        }
        return speed;
    }

    private void SetCounter(uint arg0, uint valueWord)
    {
        int index = (int)(arg0 & 0xFFFF);
        int value = ValueOf(valueWord);
        if ((arg0 & 0x2000000) != 0) value = World.Rng.GetRandom_18C018(value); // Amedo
        if ((arg0 & 0x1000000) == 0) { World.GlobalCounters[index] = value; return; }
        if ((arg0 & 0x4000000) != 0) // Amedo 2026-09-27
        {
            foreach (var link in Links)
                if (link is not null) link.Counters[index] = value & 0xFF;
            return;
        }
        CounterTarget(arg0).Counters[index] = value & 0xFF;
    }

    private void ModifyCounter(uint arg0, uint valueWord, uint filterWord)
    {
        int index = (int)(arg0 & 0xFFFF);
        int value = RootController().ValueOf(valueWord); // Amedo
        if ((arg0 & 0x1000000) == 0) { World.GlobalCounters[index] = World.GlobalCounters.GetValueOrDefault(index) + value; return; }
        if ((arg0 & 0x2000000) != 0) { ModifyLinkedCounters_212528(index, value, filterWord); return; } // Amedo
        var target = CounterTarget(arg0);
        target.Counters[index] = Math.Clamp(target.Counters.GetValueOrDefault(index) + value, 0, 255);
    }

    private AgentLabAgent CounterTarget(uint arg0) => CodeObject((int)((arg0 >> 16) & 0xFF)) ?? this;

    // Amedo 2026-09-29
    private void SetGlobalProgression_1203B8(uint arg0, uint packed)
    {
        int v = arg0 == 0xFFFFFFFF ? ValueOf(packed) : (int)arg0;
        if (0x18 < v) return;
        uint old = World.Progression;
        uint p = (old & 0xFFE0FFFF) | (((uint)v & 0x1F) << 16);
        World.Progression = p;
        if (v <= (int)((old >> 26) & 0x1F)) return;
        World.Progression = (p & 0x83FFFFFF) | (((uint)v & 0x1F) << 26);
    }

    private void SetGlobalProgression2_120590(uint arg0, uint packed)
    {
        int v = arg0 == 0xFFFFFFFF ? ValueOf(packed) : (int)arg0;
        uint p = World.Progression;
        if (v <= (int)((p >> 21) & 0x1F)) return;
        World.Progression = (p & 0xFC1FFFFF) | (((uint)v & 0x1F) << 21);
    }

    // Amedo 2026-09-27
    private void SetPlayerInput(uint arg0, uint flags)
    {
        if ((IsCharacterObject ? OwnCharacter : World.PlayerCharacter) is not { } c) return; // Amedo 2026-09-29
        uint mask = arg0 & 0xFFFF, off = arg0 >> 16;
        for (int bit = 0; bit <= 8; bit++)
        {
            uint b = 1u << bit;
            if ((mask & b) == 0) continue;
            bool on = (off & b) != 0;
            switch (bit)
            {
                case <= 5: c.InputDisabled = on ? c.InputDisabled & ~b : c.InputDisabled | b; break;
                case 6:
                    if (on) c.InputDisabled = 0;
                    else if ((flags & 1) == 0) c.InputDisabled = 0xFFFFFFFF;
                    break;
                case 7: c.MotionFlags = (c.MotionFlags & ~Character.CharacterController.GravityOn) | (on ? Character.CharacterController.GravityOn : 0); break;
                case 8: c.MotionWord = (c.MotionWord & ~0x400) | (on ? 0 : 0x400); break;
            }
        }
        c.AutomaticInput = (flags & 1) != 0; // Amedo 2026-09-28
        c.State70 = (flags & 2) != 0 ? c.State70 | 0x8000ul : c.State70 & ~0x8000ul;
    }
}
