/*
 * Inferno Collection Ladders Reborn 1.13 Alpha
 * 
 * Copyright (c) 2019-2022, Christopher M, Inferno Collection. All rights reserved.
 * 
 * This project is licensed under the following:
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to use, copy, modify, and merge the software, under the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 * The software may not be sold in any format.
 * Modified copies of the software may only be shared in an uncompiled format.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using CitizenFX.Core;
using CitizenFX.Core.Native;
using CitizenFX.Core.UI;
using InfernoCollection.LaddersReborn.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfernoCollection.LaddersReborn.Client
{
    public class Main : ClientScript
    {
        #region Configuration Variables
        internal readonly Model LADDER_MODEL = new(Globals.LADDER_PROP_NAME);

        internal readonly Vector3 LADDER_GET_ON_OFFSET_TOP = new(0f, 0.1f, 1.3f);
        internal readonly Vector3 LADDER_GET_ON_OFFSET_BOTTOM = new(0f, 0.1f, -2.7f);

        internal readonly Vector3 LADDER_GET_OFF_OFFSET_TOP = new(0f, 0.5f, 4f);
        internal readonly Vector3 LADDER_GET_OFF_OFFSET_BOTTOM = new(0f, -1f, -1.3f);

        internal const string GET_ON_LADDER_FROM_TOP_ANIM_DICT = "laddersbase";
        internal const string GET_ON_LADDER_FROM_TOP_ANIM_NAME = "get_on_top_front";

        internal const string GET_OFF_LADDER_FROM_TOP_ANIM_DICT = "laddersbase";
        internal const string GET_OFF_LADDER_FROM_TOP_ANIM_NAME = "get_off_bottom_front_stand";

        internal const string GET_ON_LADDER_FROM_BOTTOM_ANIM_DICT = "laddersbase";
        internal const string GET_ON_LADDER_FROM_BOTTOM_ANIM_NAME = "get_on_bottom_front_stand_high";

        internal const string GET_OFF_LADDER_FROM_BOTTOM_ANIM_DICT = "laddersbase";
        internal const string GET_OFF_LADDER_FROM_BOTTOM_ANIM_NAME = "get_off_top_back_stand_left_hand";

        internal const string CLIMB_UP_LADDER_ANIM_DICT = "laddersbase";
        internal const string CLIMB_UP_LADDER_ANIM_NAME = "climb_up";

        internal const string CLIMB_DOWN_LADDER_ANIM_DICT = "laddersbase";
        internal const string CLIMB_DOWN_LADDER_ANIM_NAME = "climb_down";

        internal const string PREVIEW_TOGGLE_SOUND_SET = "HUD_FRONTEND_DEFAULT_SOUNDSET";
        internal const string PREVIEW_TOGGLE_YES_SOUND = "YES";
        internal const string PREVIEW_TOGGLE_NO_SOUND = "NO";

        internal readonly IReadOnlyList<Control> CARRYING_DISABLED_CONTROLS = new List<Control>
        {
            Control.Pickup,
            Control.Enter,
            Control.Aim,
            Control.Attack,
            Control.Cover,
            Control.MeleeAttack1,
            Control.MeleeAttack2,
            Control.MeleeAttackAlternate,
            Control.MeleeAttackHeavy,
            Control.MeleeAttackLight,
            Control.Attack,
            Control.Attack2,
            Control.MpTextChatTeam
        };

        internal readonly IReadOnlyList<Control> CLIMBING_DISABLED_CONTROLS = new List<Control>
        {
            Control.Pickup,
            Control.Sprint,
            Control.Jump,
            Control.Enter,
            Control.Attack,
            Control.Attack2,
            Control.Aim,
            Control.MoveLeft,
            Control.MoveLeftOnly,
            Control.MoveLeftRight,
            Control.MoveRight,
            Control.MoveRightOnly,
            Control.MoveUp,
            Control.MoveUpDown,
            Control.MoveUpOnly,
            Control.MoveDown,
            Control.MoveDownOnly,
            Control.MeleeAttack1,
            Control.MeleeAttack2,
            Control.MeleeAttackAlternate,
            Control.MeleeAttackHeavy,
            Control.MeleeAttackLight
        };
        #endregion

        #region General Variables
        internal Config _config;

        internal Prop _previewLadder;

        internal Entity _carryingLadder;
        internal Entity _climbingLadder;

        internal bool _pauseClimbing;
        internal bool _nearClimbableLadder;
        internal bool _enablePreviewLadder = true;
        #endregion

        #region Constructor
        public Main()
        {
            #region Load Configuration File
            string configFile = null;

            try
            {
                configFile = API.LoadResourceFile("inferno-ladders-reborn", Globals.CONFIG_FILE_NAME);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Error loading configuration from file, could not load file contents. Reverting to default configuration values.");
                Debug.WriteLine(exception.ToString());
            }

            if (configFile is null || string.IsNullOrEmpty(configFile))
            {
                Debug.WriteLine("Loaded configuration file is empty, reverting to defaults.");
                return;
            }

            try
            {
                _config = JsonConvert.DeserializeObject<Config>(configFile);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Error loading configuration from file, contents are invalid. Reverting to default configuration values.");
                Debug.WriteLine(exception.ToString());
            }
            #endregion

            TriggerEvent(_config.ChatSuggestion.EventName, _config.ChatSuggestion.Command, _config.ChatSuggestion.Suggestion);
        }
        #endregion

        #region Event Handlers
        [EventHandler("onResourceStop")]
        internal void OnResourceStop(string resourceName)
        {
            if (API.GetCurrentResourceName() == resourceName)
            {
                DeletePreviewLadder();

                foreach (Prop prop in World.GetAllProps().Where(i => i.Model == LADDER_MODEL))
                {
                    try
                    {
                        prop.Delete();
                    }
                    catch
                    {
                        //
                    }
                }
            }
        }

        [EventHandler("Inferno-Collection:Client:Ladders:Notification")]
        internal void OnNotification(string message, bool blink) => Notification(message, blink);

        [EventHandler("Inferno-Collection:Client:Ladders:Collect")]
        internal async void OnCollect(int networkId)
        {
            int timeout = 20;

            while (timeout > 0 && !API.NetworkDoesNetworkIdExist(networkId))
            {
                timeout--;

                await Delay(100);
            }

            Entity entity = GetEntityFromNetwork(networkId);

            if (!Entity.Exists(entity))
            {
                return;
            }

            _carryingLadder = entity;

            Tick += CarryingTick;
        }

        [EventHandler("Inferno-Collection:Client:Ladders:Attach")]
        internal async void OnAttach(int playerId, int networkId, bool onBack = false)
        {
            int timeout = 20;

            while (timeout > 0 && !API.NetworkDoesNetworkIdExist(networkId))
            {
                timeout--;

                await Delay(100);
            }

            Entity entity = GetEntityFromNetwork(networkId);

            if (!Entity.Exists(entity))
            {
                return;
            }

            Ped ped;
            Player player = Players[playerId];

            if (player is null || (ped = player.Character) is null)
            {
                return;
            }

            if (!onBack)
            {
                Vector3 rotation = API.GetWorldRotationOfEntityBone(entity.Handle, ped.Bones["BONETAG_NECK"].Index);

                entity.AttachTo(entityBone: ped.Bones["BONETAG_R_HAND"], rotation: rotation + new Vector3(20.0f, 50.0f, 90.0f));
            }
            else
            {
                entity.AttachTo(ped.Bones[Bone.SKEL_Spine0], new(0.3f, -0.15f, 0f));
            }
        }

        [EventHandler("Inferno-Collection:Client:Ladders:Detach")]
        internal void OnDetach(int playerId, int networkId, bool beingPlaced = false)
        {
            Entity entity = GetEntityFromNetwork(networkId);

            if (!Entity.Exists(entity))
            {
                return;
            }

            Player player = Players[playerId];

            if (player is null || player.Character is null)
            {
                return;
            }

            entity.Detach();
            entity.IsPositionFrozen = false;

            if (!beingPlaced)
            {
                entity.Velocity = new(0f, 0f, -0.5f);
            }
        }

        [EventHandler("Inferno-Collection:Client:Ladders:ClimbLadder")]
        internal async void OnClimbLadder(int networkId, ClimbingDirection direction)
        {
            Entity ladder = GetEntityFromNetwork(networkId);

            if (!Entity.Exists(ladder))
            {
                return;
            }

            Ped ped = Game.PlayerPed;

            ped.Task.ClearAllImmediately();
            ped.IsPositionFrozen = true;

            _climbingLadder = ladder;
            _pauseClimbing = false;

            Tick += ClimbingTick;

            ped.Position = ladder.GetOffsetPosition(direction == ClimbingDirection.Up ? LADDER_GET_ON_OFFSET_BOTTOM : LADDER_GET_ON_OFFSET_TOP);
            ped.Task.PlayAnimation
            (
                direction == ClimbingDirection.Up ? GET_ON_LADDER_FROM_BOTTOM_ANIM_DICT : GET_ON_LADDER_FROM_TOP_ANIM_DICT,
                direction == ClimbingDirection.Up ? GET_ON_LADDER_FROM_BOTTOM_ANIM_NAME : GET_ON_LADDER_FROM_TOP_ANIM_NAME,
                2.0f, -1, AnimationFlags.Loop
            );

            for (int i = 4; i > 0; i--)
            {
                await Delay(1000);

                if (_pauseClimbing)
                {
                    int pedHandle = ped.Handle;

                    API.SetEntityAnimSpeed(pedHandle,
                        direction == ClimbingDirection.Up ? CLIMB_UP_LADDER_ANIM_DICT : CLIMB_DOWN_LADDER_ANIM_DICT,
                        direction == ClimbingDirection.Up ? CLIMB_UP_LADDER_ANIM_NAME : CLIMB_DOWN_LADDER_ANIM_NAME,
                    0.0f);

                    while (_pauseClimbing)
                    {
                        await Delay(500);
                    }
                }

                if (_climbingLadder is null)
                {
                    ped.IsPositionFrozen = false;
                    ped.Task.ClearAllImmediately();
                    return;
                }

                ped.Position = ped.GetOffsetPosition(direction == ClimbingDirection.Up ? new(0f, 0.3f, 0.01f) : new (0f, 0.3f, -2f));
                ped.Task.PlayAnimation
                (
                    direction == ClimbingDirection.Up ? CLIMB_UP_LADDER_ANIM_DICT : CLIMB_DOWN_LADDER_ANIM_DICT,
                    direction == ClimbingDirection.Up ? CLIMB_UP_LADDER_ANIM_NAME : CLIMB_DOWN_LADDER_ANIM_NAME,
                    2.0f, -1, AnimationFlags.Loop
                );
            }

            ped.Position = ladder.GetOffsetPosition(direction == ClimbingDirection.Up ? LADDER_GET_ON_OFFSET_TOP : LADDER_GET_ON_OFFSET_BOTTOM);
            ped.Task.PlayAnimation
            (
                direction == ClimbingDirection.Up ? GET_OFF_LADDER_FROM_BOTTOM_ANIM_DICT : GET_OFF_LADDER_FROM_TOP_ANIM_DICT,
                direction == ClimbingDirection.Up ? GET_OFF_LADDER_FROM_BOTTOM_ANIM_NAME : GET_OFF_LADDER_FROM_TOP_ANIM_NAME,
                2.0f, -1, AnimationFlags.Loop
            );

            await Delay(1000);

            if (_climbingLadder is null)
            {
                ped.IsPositionFrozen = false;
                ped.Task.ClearAllImmediately();
                return;
            }

            ped.Position = ladder.GetOffsetPosition(direction == ClimbingDirection.Up ? LADDER_GET_OFF_OFFSET_TOP : LADDER_GET_OFF_OFFSET_BOTTOM);
            ped.Task.ClearAllImmediately();
            ped.IsPositionFrozen = false;

            Tick -= ClimbingTick;

            _climbingLadder = null;
            _pauseClimbing = false;

            TriggerServerEvent("Inferno-Collection:Server:Ladders:FinishedClimbing", networkId);
        }
        #endregion

        #region Tick Handlers
        [Tick]
        internal async Task LadderTick()
        {
            _nearClimbableLadder = false;

            Vector3 pedPosition = Game.PlayerPed.Position;
            Entity entity = World.GetAllProps()
                .Where(i => i.Model == LADDER_MODEL)
                .OrderBy(i => World.GetDistance(i.Position, pedPosition))
                .FirstOrDefault(i => i != _carryingLadder);

            if (!Entity.Exists(entity))
            {
                await Delay(3000);
                return;
            }

            float distanceToLadder = World.GetDistance(pedPosition, entity.Position);

            if (distanceToLadder > 10f)
            {
                await Delay(2000);
                return;
            }
            else if (distanceToLadder > 5f)
            {
                await Delay(1000);
                return;
            }

            switch ((Status)(entity.State.Get("ICLadderStatus") ?? -1))
            {
                case Status.Placed:
                    if (World.GetDistance(pedPosition, entity.GetOffsetPosition(new(0f, 0f, 1.2f))) > 3.5f)
                    {
                        return;
                    }

                    foreach (Control control in new[] { Control.Enter, Control.Pickup })
                    {
                        Game.DisableControlThisFrame(0, control);
                    }                    

                    float distanceToTop = World.GetDistance(pedPosition, entity.Position + new Vector3(0f, 0f, 5f));
                    float distanceToBottom = World.GetDistance(pedPosition, entity.Position + new Vector3(0f, 0f, -5f));

                    _nearClimbableLadder = true;

                    string helpText = "~INPUT_PICKUP~ Climb ladder";

                    if (_carryingLadder is null)
                    {
                        helpText += "\n~INPUT_ENTER~ Pick up ladder";
                    }

                    Screen.DisplayHelpTextThisFrame(helpText);
                    
                    if (Game.IsDisabledControlJustPressed(0, Control.Pickup))
                    {
                        TriggerServerEvent("Inferno-Collection:Server:Ladders:ClimbLadder", entity.NetworkId, distanceToTop > distanceToBottom ? ClimbingDirection.Up : ClimbingDirection.Down);

                        await Delay(3000);
                    }
                    else if (_carryingLadder is null && Game.IsDisabledControlJustPressed(0, Control.Enter))
                    {
                        PickUpLadder(entity);

                        await Delay(3000);
                    }
                    return;

                case Status.Dropped:
                    if (distanceToLadder > 1.5f)
                    {
                        return;
                    }

                    Game.DisableControlThisFrame(0, Control.Pickup);

                    Screen.DisplayHelpTextThisFrame("~INPUT_PICKUP~ Pick up ladder");

                    if (Game.IsDisabledControlJustPressed(0, Control.Pickup))
                    {
                        PickUpLadder(entity);

                        await Delay(3000);
                    }
                    return;

                default:
                    await Delay(1000);
                    return;
            }
        }

        internal async Task CarryingTick()
        {
            if (!Entity.Exists(_carryingLadder))
            {
                DeletePreviewLadder();

                Tick -= CarryingTick;
                return;
            }

            Ped ped = Game.PlayerPed;

            // If Client jumps off a ledge or similar while carrying a ladder
            if (ped.CurrentVehicle is not null || !PedCanDoAction(ped))
            {
                DeletePreviewLadder();

                await Delay(750);

                if (!PedCanDoAction(ped))
                {
                    Notification("~y~You dropped your ladder!", true);

                    DropLadder();

                    await Delay(3000);
                }
                return;
            }

            foreach (Control control in CARRYING_DISABLED_CONTROLS)
            {
                Game.DisableControlThisFrame(0, control);
            }

            DeletePreviewLadder();

            if (_nearClimbableLadder || _climbingLadder is not null)
            {
                await Delay(1000);
                return;
            }

            if (_config.PreviewLadderMode == PreviewLadderMode.ForcedPreview || (_config.PreviewLadderMode == PreviewLadderMode.OptionalPreview && _enablePreviewLadder))
            {
                Vector3 pedPosition = ped.Position;
                RaycastResult raycast = World.RaycastCapsule(pedPosition, pedPosition, 2f, (IntersectOptions)10, ped);

                if (!raycast.DitHitEntity)
                {
                    Vector3 previewCoords = ped.GetOffsetPosition(new(0.0f, 1.2f, 1.32f));
                    _previewLadder = new Prop(API.CreateObjectNoOffset((uint)LADDER_MODEL.Hash, previewCoords.X, previewCoords.Y, previewCoords.Z, false, false, false))
                    {
                        Rotation = ped.Rotation + new Vector3(-20f, 0f, 0f),
                        IsCollisionEnabled = false,
                        Opacity = 100
                    };
                }
            }

            string helpText = "~INPUT_PICKUP~ Place ladder\n~INPUT_ENTER~ Drop ladder";

            if (_config.PreviewLadderMode == PreviewLadderMode.OptionalPreview)
            {
                helpText += "\n~INPUT_MP_TEXT_CHAT_TEAM~ Toggle preview";
            }

            Screen.DisplayHelpTextThisFrame(helpText);

            if (_config.PreviewLadderMode == PreviewLadderMode.OptionalPreview && Game.IsDisabledControlJustPressed(0, Control.MpTextChatTeam))
            {
                _enablePreviewLadder = !_enablePreviewLadder;

                Game.PlaySound(PREVIEW_TOGGLE_SOUND_SET, _enablePreviewLadder ? PREVIEW_TOGGLE_YES_SOUND : PREVIEW_TOGGLE_NO_SOUND);
            }
            else if (Game.IsDisabledControlJustPressed(0, Control.Pickup))
            {
                Tick -= CarryingTick;

                _carryingLadder = null;

                TriggerServerEvent("Inferno-Collection:Server:Ladders:PlaceLadder");

                DeletePreviewLadder();
            }
            else if (Game.IsDisabledControlJustPressed(0, Control.Enter))
            {
                DropLadder();
            }

            await Task.FromResult(0);
        }

        internal async Task ClimbingTick()
        {
            Game.PlayerPed.Rotation = _climbingLadder.Rotation;

            foreach (Control control in CLIMBING_DISABLED_CONTROLS)
            {
                Game.DisableControlThisFrame(0, control);
            }

            Screen.DisplayHelpTextThisFrame($"~INPUT_JUMP~ {(_pauseClimbing ? "Continue" : "Pause")} climbing\n~INPUT_PICKUP~ Cancel climbing");

            if (Game.IsDisabledControlJustPressed(0, Control.Jump))
            {
                _pauseClimbing = !_pauseClimbing;
            }
            else if (Game.IsDisabledControlJustPressed(0, Control.Pickup))
            {
                TriggerServerEvent("Inferno-Collection:Server:Ladders:FinishedClimbing", _climbingLadder.NetworkId);

                _pauseClimbing = false;
                _climbingLadder = null;

                Tick -= ClimbingTick;
                return;
            }

            await Task.FromResult(0);
        }
        #endregion

        #region Helper Functions
        internal void PickUpLadder(Entity entity) => TriggerServerEvent("Inferno-Collection:Server:Ladders:PickUpLadder", entity.NetworkId);

        internal bool PedCanDoAction(Ped ped) => !(ped.IsCuffed || ped.IsBeingStunned || ped.IsClimbing || ped.IsDiving || ped.IsFalling || ped.IsGettingIntoAVehicle || ped.IsJumping || ped.IsJumpingOutOfVehicle || ped.IsRagdoll || ped.IsSwimmingUnderWater || ped.IsVaulting);

        internal void DeletePreviewLadder()
        {
            if (Entity.Exists(_previewLadder))
            {
                _previewLadder.Delete();
                _previewLadder = null;
            }
        }

        internal void DropLadder()
        {
            Tick -= CarryingTick;

            _carryingLadder = null;

            DeletePreviewLadder();

            TriggerServerEvent("Inferno-Collection:Server:Ladders:PlaceLadder", true);
        }

        internal Entity GetEntityFromNetwork(int networkId)
        {
            if (networkId == 0 || !API.NetworkDoesNetworkIdExist(networkId))
            {
                Debug.WriteLine($"Could not request network ID {networkId} because it does not exist!");
                return null;
            }

            return Entity.FromNetworkId(networkId);
        }

        internal void Notification(string message, bool blink)
        {
            if (!_config.CustomNotifications)
            {
                Screen.ShowNotification(message, blink);
            }
            else
            {
                TriggerEvent(_config.CustomNotificationEventName, message, blink);
            }
        }
        #endregion
    }
}