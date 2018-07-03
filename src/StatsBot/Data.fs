namespace StatsBot

module Data=
    open System.Net.Http
    open Newtonsoft.Json.Linq
    
    type GetJson = string -> Async<string>

    let private client() = new HttpClient()
    
    let private getJson(client: HttpClient) (uri: string)=
        async {
                     
            let! resp = client.GetAsync(uri) |> Async.AwaitTask

            if resp.StatusCode <> System.Net.HttpStatusCode.OK then
                resp.StatusCode.ToString() |> sprintf "Error %s returned from ESI" |> failwith

            let! json = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                
            return json
        }

    let getFwSystems(getJson: GetJson)=
        async {
                let! json = getJson "https://esi.evetech.net/latest/fw/systems/?datasource=tranquility"
                let xs = Newtonsoft.Json.JsonConvert.DeserializeObject(json) :?> Newtonsoft.Json.Linq.JArray

                let systemIds = xs |> Seq.map (fun x -> x :?> Newtonsoft.Json.Linq.JObject) 
                                    |> Seq.map (fun o -> o.["solar_system_id"].ToObject<int32>())
                                    |> Set.ofSeq
                
                return systemIds
            }

    let getIncursionSystems(getJson: GetJson) =
        async {
                let! json = getJson "https://esi.evetech.net/latest/incursions/?datasource=tranquility"
                let xs = Newtonsoft.Json.JsonConvert.DeserializeObject(json) :?> Newtonsoft.Json.Linq.JArray

                let systemIds = xs |> Seq.map (fun x -> x :?> Newtonsoft.Json.Linq.JObject) 
                                    |> Seq.collect (fun o -> o.["infested_solar_systems"])
                                    |> Seq.map (fun v -> v :?> JValue)
                                    |> Seq.map (fun v -> v.ToObject<int32>())
                                    |> Set.ofSeq
                
                return systemIds
            }
    
    let getKills (getJson: GetJson) (incursionSystems: Set<int32>) (fwSystems: Set<int32>)=
        async { 
                let! json = getJson "https://esi.evetech.net/latest/universe/system_kills/?datasource=tranquility"
                return Newtonsoft.Json.JsonConvert.DeserializeObject(json) :?> Newtonsoft.Json.Linq.JArray
                    |> Seq.map (fun s -> 
                                        let systemId = s.["system_id"].ToObject<int32>()                                   
                                
                                        let systemName, regionName, level = match systemId |> IronSde.SolarSystems.ofId with
                                                                            | Some s -> match s |> (fun s -> s.regionId) |> IronSde.Regions.ofId with
                                                                                        | Some r -> s.name, r.name, s.level.ToString()
                                                                                        | _ -> s.name, (sprintf "Region%i" s.regionId), s.level.ToString()
                                                                            | _ -> (sprintf "System%i"  systemId), "Unknown", "Unknown"
                                
                                
                                        { SystemStats.name = systemName;
                                                    level = level;
                                                    regionName = regionName;
                                                    systemId = systemId;
                                                    jumps = 0;
                                                    npcKills = s.["npc_kills"].ToObject<int32>();
                                                    podKills= s.["pod_kills"].ToObject<int32>();
                                                    shipKills = s.["ship_kills"].ToObject<int32>();
                                                    isIncursion = incursionSystems.Contains(systemId);
                                                    isFw = fwSystems.Contains(systemId);
                                            }
                                    )
                    |> Array.ofSeq
            }
    
    let getJumps(getJson: GetJson)=
        async { 
                let! json = getJson "https://esi.evetech.net/latest/universe/system_jumps/?datasource=tranquility"
                
                return Newtonsoft.Json.JsonConvert.DeserializeObject(json) :?> Newtonsoft.Json.Linq.JArray
                                |> Seq.map (fun s ->    let systemId = s.["system_id"].ToObject<int32>()
                                                        let jumps = s.["ship_jumps"].ToObject<int32>()
                                                        {SystemJumps.systemId= systemId; jumps = jumps})
                                |> Array.ofSeq 
                }

    let joinJumps(jumps: seq<SystemJumps>) (kills: SystemStats []) =
        let jumps = jumps 
                        |> Seq.map (fun s ->    s.systemId, s.jumps)
                        |> Map.ofSeq
        
        let result = kills  |> Seq.filter (fun k -> jumps.ContainsKey(k.systemId))
                            |> Seq.map (fun k ->    let j = jumps.[k.systemId] 
                                                    { k with jumps = j } )
                            |> Array.ofSeq
        result    

    let getData() =
        async {
            use client = client()
            let getJson = getJson client

            let! inc = getIncursionSystems getJson |> Async.StartChild
            let! fw = getFwSystems getJson |> Async.StartChild
            let! js = getJumps getJson |> Async.StartChild

            let! incursionSystems = inc
            let! fwSystems = fw
            
            let! kills = getKills getJson incursionSystems fwSystems
            
            let! jumps = js

            return joinJumps jumps kills
            }
