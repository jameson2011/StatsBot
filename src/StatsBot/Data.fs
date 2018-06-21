namespace StatsBot

module Data=
    
    let getJson(uri: string)=
        async {
            use client = new System.Net.Http.HttpClient()
                        
            let! resp = client.GetAsync(uri) |> Async.AwaitTask

            if resp.StatusCode <> System.Net.HttpStatusCode.OK then
                resp.StatusCode.ToString() |> sprintf "Error %s returned from ESI" |> failwith

            let! json = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                
            return json
        }

    let getKillsJson()=
        getJson "https://esi.evetech.net/latest/universe/system_kills/?datasource=tranquility"
    
    let getKillsStats(json: string)=
        Newtonsoft.Json.JsonConvert.DeserializeObject(json) :?> Newtonsoft.Json.Linq.JArray
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
                                            
                                    }
                            )
            |> Array.ofSeq
        

    let getJumpsJson()=
        getJson "https://esi.evetech.net/latest/universe/system_jumps/?datasource=tranquility"

    let joinJumps(jumpsJson: string) (kills: SystemStats []) =
        let jumps = Newtonsoft.Json.JsonConvert.DeserializeObject(jumpsJson) :?> Newtonsoft.Json.Linq.JArray
                        |> Seq.map (fun s ->    let systemId = s.["system_id"].ToObject<int32>()
                                                let jumps = s.["ship_jumps"].ToObject<int32>()
                                                systemId, jumps)
                        |> Map.ofSeq
        
        let result = kills  |> Seq.filter (fun k -> jumps.ContainsKey(k.systemId))
                            |> Seq.map (fun k ->    let j = jumps.[k.systemId] 
                                                    { k with jumps = j } )
                            |> Array.ofSeq
        result    

    let getData() =
        let kills = getKillsJson() |> Async.RunSynchronously |> getKillsStats
        let jumpsJson = getJumpsJson() |> Async.RunSynchronously

        joinJumps jumpsJson kills

