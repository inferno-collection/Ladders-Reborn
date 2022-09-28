/*
 * Inferno Collection Ladders Reborn 1.12 Alpha
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
using InfernoCollection.LaddersReborn.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InfernoCollection.LaddersReborn.Server
{
    public class Main : ServerScript
    {
        #region Configuration Variables
        internal readonly uint LADDER_PROP = (uint)API.GetHashKey(Globals.LADDER_PROP_NAME);
        #endregion

        #region General Variables
        internal Config _config;

        internal readonly List<int> _createdLadders = new();

        internal Dictionary<int, int> _whitelistedModels = new();
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

            if (_config.EnableLadderVehicleWhitelist)
            {
                foreach (KeyValuePair<string, int> kvp in _config.LadderVehicleWhitelist)
                {
                    int model = API.GetHashKey(kvp.Key);
                    int maxLadderCount = kvp.Value;

                    _whitelistedModels.Add(model, maxLadderCount);
                }
            }
        }
        #endregion

        #region Command Handlers
        [Command("ladder")]
        internal void OnLadder([FromSource] Player source) => Ladder(source);
        #endregion

        #region Event Handlers
        [EventHandler("onResourceStop")]
        internal void OnResourceStop(string resourceName)
        {
            if (API.GetCurrentResourceName() == resourceName)
            {
                DeleteAllLadders();
            }

            foreach (Player player in Players)
            {
                player.State.Set("ICCarryingLadder", -1, true);
            }

            var vehiclesHandles = API.GetAllVehicles();

            foreach (int handle in vehiclesHandles)
            {
                try
                {
                    Entity entity = Entity.FromHandle(handle);

                    if (entity.State.Get("ICLaddersStored") is not null)
                    {
                        entity.State.Set("ICLaddersStored", null, true);
                    }
                }
                catch
                {
                    //
                }
            }
        }

        [EventHandler("Inferno-Collection:Server:Ladders:ToggleLadder")]
        internal void OnToggleLadder([FromSource] Player source) => Ladder(source);

        [EventHandler("Inferno-Collection:Server:Ladders:CollectLadder")]
        internal void OnCollectLadder([FromSource] Player source) => CollectLadder(source);

        [EventHandler("Inferno-Collection:Server:Ladders:StoreLadder")]
        internal void OnStoreLadder([FromSource] Player source) => StoreLadder(source);

        [EventHandler("Inferno-Collection:Server:Ladders:PlaceLadder")]
        internal async void OnCreateLadder([FromSource] Player source, bool dropped)
        {
            try
            {
                int networkId = source.State.Get("ICCarryingLadder") ?? -1;

                if (networkId < 0)
                {
                    return;
                }

                Entity entity = Entity.FromNetworkId(networkId);

                if (!API.DoesEntityExist(entity.Handle))
                {
                    return;
                }

                StateBag stateBag = entity.State;

                if ((Status)(stateBag.Get("ICLadderStatus") ?? -1) != Status.BeingCarried)
                {
                    return;
                }

                (entity.Owner ?? source).TriggerEvent("Inferno-Collection:Client:Ladders:Detach", int.Parse(source.Handle), entity.NetworkId, !dropped);

                int timeout = 20;

                while (timeout > 0 && API.GetEntityAttachedTo(entity.Handle) != 0)
                {
                    timeout--;

                    await Delay(100);
                }

                if (!dropped)
                {
                    Ped ped = source.Character;

                    entity.Position = GetOffsetFromEntityInWorldCoords(ped, new(0.0f, 1.2f, 2.3f));
                    entity.Rotation = ped.Rotation + new Vector3(-20.0f, 0f, 0f);
                    entity.IsPositionFrozen = true;
                }

                source.State.Set("ICCarryingLadder", -1, true);

                stateBag.Set("ICLadderStatus", (int)(dropped ? Status.Dropped : Status.Placed), true);
            }
            catch
            {
                Debug.WriteLine($"Error placing ladder for {source.Name}");
            }
        }

        [EventHandler("Inferno-Collection:Server:Ladders:ClimbLadder")]
        internal void OnUpdateLadder([FromSource] Player source, int networkId, ClimbingDirection direction)
        {
            try
            {
                Entity entity = Entity.FromNetworkId(networkId);

                if (!API.DoesEntityExist(entity.Handle))
                {
                    return;
                }

                if (!_createdLadders.Contains(entity.Handle))
                {
                    HandleCheater(source, $"!!! {source.Name} (#{source.Handle}) has been caught trying to use \"Inferno-Collection:Server:Ladders:ClimbLadder\" on an unrelated entity.");
                    return;
                }

                StateBag stateBag = entity.State;

                if ((Status)(stateBag.Get("ICLadderStatus") ?? 0) != Status.Placed)
                {
                    return;
                }

                stateBag.Set("ICLadderStatus", (int)Status.BeingClimbed, true);

                source.TriggerEvent("Inferno-Collection:Client:Ladders:ClimbLadder", networkId, direction);

                dynamic carryingNetworkId = source.State.Get("ICCarryingLadder");

                if ((carryingNetworkId ?? -1) > 0)
                {
                    (entity.Owner ?? source).TriggerEvent("Inferno-Collection:Client:Ladders:Attach", int.Parse(source.Handle), carryingNetworkId, true);
                }
            }
            catch
            {
                Debug.WriteLine($"Error climbing ladder for {source.Name}");
            }
        }

        [EventHandler("Inferno-Collection:Server:Ladders:FinishedClimbing")]
        internal void OnFinishedClimbing([FromSource] Player source, int networkId)
        {
            try
            {
                Entity entity = Entity.FromNetworkId(networkId);

                if (!API.DoesEntityExist(entity.Handle))
                {
                    return;
                }

                if (!_createdLadders.Contains(entity.Handle))
                {
                    HandleCheater(source, $"!!! {source.Name} (#{source.Handle}) has been caught trying to use \"Inferno-Collection:Server:Ladders:FinishedClimbing\" on an unrelated entity.");
                    return;
                }

                StateBag stateBag = entity.State;

                if ((Status)(stateBag.Get("ICLadderStatus") ?? 0) != Status.BeingClimbed)
                {
                    return;
                }

                stateBag.Set("ICLadderStatus", (int)Status.Placed, true);

                dynamic carryingNetworkId = source.State.Get("ICCarryingLadder");

                if ((carryingNetworkId ?? -1) > 0)
                {
                    (entity.Owner ?? source).TriggerEvent("Inferno-Collection:Client:Ladders:Attach", int.Parse(source.Handle), carryingNetworkId);
                }
            }
            catch
            {
                Debug.WriteLine($"Error finishing climbing ladder for {source.Name}");
            }
        }

        [EventHandler("Inferno-Collection:Server:Ladders:PickUpLadder")]
        internal void OnPickUpLadder([FromSource] Player source, int networkId)
        {
            try
            {
                Entity entity = Entity.FromNetworkId(networkId);

                if (!API.DoesEntityExist(entity.Handle))
                {
                    return;
                }

                if (!_createdLadders.Contains(entity.Handle))
                {
                    HandleCheater(source, $"!!! {source.Name} (#{source.Handle}) has been caught trying to use \"Inferno-Collection:Server:Ladders:PickUpLadder\" on an unrelated entity.");
                    return;
                }

                StateBag stateBag = entity.State;

                if ((Status)(stateBag.Get("ICLadderStatus") ?? 0) < Status.Placed)
                {
                    return;
                }

                if ((source.State.Get("ICCarryingLadder") ?? -1) > 0)
                {
                    SendNotification(source, "~r~You are already carrying a ladder");
                    return;
                }

                entity.State.Set("ICLadderStatus", (int)Status.BeingCarried, true);

                source.State.Set("ICCarryingLadder", entity.NetworkId, true);
                source.TriggerEvent("Inferno-Collection:Client:Ladders:Collect", entity.NetworkId);

                (entity?.Owner ?? source).TriggerEvent("Inferno-Collection:Client:Ladders:Attach", int.Parse(source.Handle), entity.NetworkId);
            }
            catch
            {
                Debug.WriteLine($"Error picking up ladder for {source.Name}");
            }
        }
        #endregion

        #region Tick Handlers
        [Tick]
        internal async Task CleanUpTick()
        {
            int playerCount = 0;

            await Delay(30 * 1000);

            if ((_createdLadders.Count == 0 && (playerCount = Players.Count()) == 0) || playerCount > 0)
            {
                return;
            }

            await Delay(600 * 1000);

            if (_createdLadders.Count == 0 || Players.Count() > 0)
            {
                return;
            }

            DeleteAllLadders();

            Debug.WriteLine($"No players remaining after 10 minutes, cleared all ladders.");
        }
        #endregion

        #region Functions
        internal void SendNotification(Player player, string message, bool blink = false) => player.TriggerEvent("Inferno-Collection:Client:Ladders:Notification", message, blink);

        internal void Ladder(Player player)
        {
            StateBag stateBag = player.State;

            if ((stateBag.Get("ICCarryingLadder") ?? -1) > 0)
            {
                StoreLadder(player);
            }
            else if ((stateBag.Get("ICCarryingLadder") ?? -1) < 0)
            {
                if (_config.MaxTotalLadders != -1 && _createdLadders.Count >= _config.MaxTotalLadders)
                {
                    SendNotification(player, "~r~Max ladder count reached!", true);
                    return;
                }

                CollectLadder(player);
            }
        }

        internal async void CollectLadder(Player player)
        {
            if ((player.State.Get("ICCarryingLadder") ?? -1) > 0)
            {
                SendNotification(player, "~r~You are already carrying a ladder");
                return;
            }

            Ped ped = player.Character;

            if (API.GetVehiclePedIsIn(ped.Handle, false) != 0)
            {
                SendNotification(player, "~r~You cannot collect a ladder from within a vehicle");
                return;
            }

            Vehicle vehicle = GetNearByVehicle(player);

            if (vehicle is null)
            {
                SendNotification(player, "~r~No suitable vehicle found!", true);
                return;
            }

            if (_config.MaxTotalLadders != -1 && _createdLadders.Count >= _config.MaxTotalLadders)
            {
                SendNotification(player, "~r~Max ladder count reached!", true);
                return;
            }

            int model;
            dynamic stateValue = 0;
            StateBag stateBag = null;

            if (_config.EnableLadderVehicleWhitelist && _whitelistedModels[model = vehicle.Model] != -1)
            {
                stateBag = vehicle.State;

                if ((stateValue = stateBag.Get("ICLaddersStored")) is null)
                {
                    stateValue = _whitelistedModels[model];
                    stateBag.Set("ICLaddersStored", stateValue, true);
                }

                if (stateValue <= 0)
                {
                    SendNotification(player, "~r~Vehicle has no ladders left!", true);
                    return;
                }
            }

            try
            {
                int timeout = 5;
                Vector3 position = ped.Position;
                Entity entity = new Prop(API.CreateObjectNoOffset(LADDER_PROP, position.X, position.Y, position.Z - 50f, true, true, true));

                while (!API.DoesEntityExist(entity.Handle) && timeout > 0)
                {
                    timeout--;

                    await Delay(200);
                }

                if (timeout == 0)
                {
                    Debug.WriteLine($"Error spawning ladder prop for {player.Name}");
                    return;
                }

                _createdLadders.Add(entity.Handle);

                entity.State.Set("ICLadderStatus", (int)Status.BeingCarried, true);

                player.State.Set("ICCarryingLadder", entity.NetworkId, true);
                player.TriggerEvent("Inferno-Collection:Client:Ladders:Collect", entity.NetworkId);

                (entity.Owner ?? player).TriggerEvent("Inferno-Collection:Client:Ladders:Attach", int.Parse(player.Handle), entity.NetworkId);

                if (_config.EnableLadderVehicleWhitelist && _whitelistedModels[model = vehicle.Model] != -1)
                {
                    stateBag.Set("ICLaddersStored", --stateValue, true);
                }

                SendNotification(player, "~g~Ladder collected");
            }
            catch
            {
                Debug.WriteLine($"Error creating ladder prop for {player.Name}");
            }
        }

        internal void StoreLadder(Player source)
        {
            try
            {
                int networkId = source.State.Get("ICCarryingLadder") ?? -1;

                if (networkId < 0)
                {
                    SendNotification(source, "~r~You are not carrying a ladder");
                    return;
                }

                Vehicle vehicle = GetNearByVehicle(source);

                if (vehicle is null)
                {
                    SendNotification(source, "~r~No suitable vehicle found!", true);
                    return;
                }

                int model;
                dynamic stateValue = 0;
                StateBag stateBag = null;

                if (_config.EnableLadderVehicleWhitelist && _whitelistedModels[model = vehicle.Model] != -1)
                {
                    stateBag = vehicle.State;

                    if ((stateValue = stateBag.Get("ICLaddersStored")) is null)
                    {
                        stateValue = _whitelistedModels[model];
                        stateBag.Set("ICLaddersStored", stateValue, true);
                    }

                    if (stateValue >= _whitelistedModels[model])
                    {
                        SendNotification(source, "~r~Vehicle cannot carry more ladders", true);
                        return;
                    }
                }

                Entity entity = Entity.FromNetworkId(networkId);

                if (!API.DoesEntityExist(entity.Handle) || (Status)(entity.State.Get("ICLadderStatus") ?? -1) != Status.BeingCarried)
                {
                    return;
                }

                if (!_createdLadders.Contains(entity.Handle))
                {
                    HandleCheater(source, $"!!! {source.Name} (#{source.Handle}) has been caught trying to use \"Inferno-Collection:Server:Ladders:StoreLadder\" to delete an unrelated entity.");
                    return;
                }

                source.State.Set("ICCarryingLadder", -1, true);

                _createdLadders.Remove(entity.Handle);

                API.DeleteEntity(entity.Handle);

                SendNotification(source, "~g~Ladder stored");

                if (_config.EnableLadderVehicleWhitelist && _whitelistedModels[model = vehicle.Model] != -1)
                {
                    stateBag.Set("ICLaddersStored", ++stateValue, true);
                }
            }
            catch
            {
                Debug.WriteLine($"Error storing ladder for {source.Name}");
            }
        }

        internal Vehicle GetNearByVehicle(Player player)
        {
            try
            {
                Dictionary<int, float> vehicles = new();
                var vehiclesHandles = API.GetAllVehicles();
                Vector3 position = player.Character.Position;

                foreach (int handle in vehiclesHandles)
                {
                    float distance = (float)Math.Sqrt(API.GetEntityCoords(handle).DistanceToSquared(position));

                    if (distance <= _config.MaxDistanceToVehicle)
                    {
                        vehicles.Add(handle, distance);
                    }
                }

                vehicles.OrderBy(i => i.Value);

                Vehicle vehicle = vehicles.Count == 0 ? null : new Vehicle(vehicles.First().Key);

                if (_config.EnableLadderVehicleWhitelist)
                {
                    if (vehicle is not null && _whitelistedModels.ContainsKey(vehicle.Model))
                    {
                        return vehicle;
                    }

                    return null;
                }

                if (vehicle is not null && API.GetVehicleType(vehicle.Handle).ToUpper() == "AUTOMOBILE")
                {
                    return vehicle;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        internal void DeleteAllLadders()
        {
            foreach (int handle in _createdLadders)
            {
                try
                {
                    if (API.DoesEntityExist(handle))
                    {
                        API.DeleteEntity(handle);
                    }
                }
                catch
                {
                    //
                }
            }
        }

        internal void HandleCheater(Player player, string context)
        {
            Debug.WriteLine(context);

            if (_config.KickCheaters)
            {
                player.Drop("Cheating is bad!");
            }
        }

        /// <summary>
        /// Credit to draobrehtom.
        /// https://forum.cfx.re/t/p/4502297.
        /// https://forum.cfx.re/u/draobrehtom.
        /// </summary>
        internal Vector3 GetOffsetFromEntityInWorldCoords(Entity entity, Vector3 offset)
        {
            Vector3 position = entity.Position;
            Vector3 rotation = entity.Rotation;

            float rX = MathUtil.DegreesToRadians(rotation.X);
            float rY = MathUtil.DegreesToRadians(rotation.Y);
            float rZ = MathUtil.DegreesToRadians(rotation.Z);

            double cosRx = Math.Cos(rX);
            double cosRy = Math.Cos(rY);
            double cosRz = Math.Cos(rZ);
            double sinRx = Math.Sin(rX);
            double sinRy = Math.Sin(rY);
            double sinRz = Math.Sin(rZ);

            Matrix matrix = new()
            {
                M11 = (float)((cosRz * cosRy) - (sinRz * sinRx * sinRy)),
                M12 = (float)((cosRy * sinRz) + (cosRz * sinRx * sinRy)),
                M13 = (float)(-cosRx * sinRy),
                M14 = 1,

                M21 = (float)(-cosRx * sinRz),
                M22 = (float)(cosRz * cosRx),
                M23 = (float)sinRx,
                M24 = 1,

                M31 = (float)((cosRz * sinRy) + (cosRy * sinRz * sinRx)),
                M32 = (float)((sinRz * sinRy) - (cosRz * cosRy * sinRx)),
                M33 = (float)(cosRx * cosRy),
                M34 = 1,

                Row4 = new(position.X, position.Y, position.Z - 1f, 1f)
            };

            return new()
            {
                X = (offset.X * matrix.M11) + (offset.Y * matrix.M21) + (offset.Z * matrix.M31) + matrix.M41,
                Y = (offset.X * matrix.M12) + (offset.Y * matrix.M22) + (offset.Z * matrix.M32) + matrix.M42,
                Z = (offset.X * matrix.M13) + (offset.Y * matrix.M23) + (offset.Z * matrix.M33) + matrix.M43
            };
        }
        #endregion
    }
}