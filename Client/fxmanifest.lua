-- Inferno Collection Ladders Reborn 1.11 Beta
--
-- Copyright (c) 2019-2022, Christopher M, Inferno Collection. All rights reserved.
--
-- This project is licensed under the following:
-- Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to use, copy, modify, and merge the software, under the following conditions:
-- The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
-- The software may not be sold in any format.
-- Modified copies of the software may only be shared in an uncompiled format.
-- THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

name "Ladders Reborn - Inferno Collection"

description "OneSync Ladder script for FiveM."

author "Inferno Collection (inferno-collection.com)"

version "1.11 Beta"

url "https://inferno-collection.com"

client_script {
    "LaddersReborn.Client.net.dll"
}

server_script {
    "LaddersReborn.Server.net.dll"
}

files {
    "config.json",
    "stream/props.ytyp",
    "Newtonsoft.Json.dll"
}

data_file "DLC_ITYP_REQUEST" "stream/props.ytyp"

game "gta5"

fx_version "cerulean"

dependency "/onesync"

provide "inferno-ladders"