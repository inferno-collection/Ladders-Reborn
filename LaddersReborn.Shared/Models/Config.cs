/*
 * Inferno Collection Ladders Reborn 1.13 Beta
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

using System.Collections.Generic;

namespace InfernoCollection.LaddersReborn.Models
{
    public class Config
    {
        public ChatSuggestion ChatSuggestion { get; set; } = new ChatSuggestion();

        public bool CustomNotifications { get; set; } = false;
        public string CustomNotificationEventName { get; set; } = "";

        public PreviewLadderMode PreviewLadderMode { get; set; } = PreviewLadderMode.OptionalPreview;

        public bool KickCheaters { get; set; } = false;

        public float MaxDistanceToVehicle { get; set; } = 4f;

        public bool EnableLadderVehicleWhitelist { get; set; } = false;
        public IReadOnlyDictionary<string, int> LadderVehicleWhitelist { get; set; } = new Dictionary<string, int>();

        public int MaxTotalLadders { get; set; } = -1;
    }

    public class ChatSuggestion
    {
        public string EventName { get; set; } = "chat:addSuggestion";
        public string Command { get; set; } = "/ladder";
        public string Suggestion { get; set; } = "Collects or stores a ladder in the closest vehicle.";
    }

    public enum PreviewLadderMode
    {
        ForcedNoPreview,
        OptionalPreview,
        ForcedPreview
    }
}