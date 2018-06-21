namespace StatsBot

module Program=

    open System.Text
    open Discord
    
    
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
                                let system = systemId |> IronSde.SolarSystems.ofId |> Option.get
                                let region = system |> (fun s -> s.regionId) |> IronSde.Regions.ofId |> Option.get
                                
                                { SystemStats.name = system.name;
                                            level = system.level.ToString();
                                            regionName = region.name;
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

    let writeCsv(output: string) (stats: SystemStats []) =
        use writer = new System.IO.StreamWriter(output, false, Encoding.UTF8)
        use csv = new CsvHelper.CsvWriter(writer)
            
        csv.WriteHeader(typeof<SystemStats>)
        csv.NextRecord()

        stats |> Seq.iter (fun s -> csv.WriteRecord(s)
                                    csv.NextRecord())
            

    let sendToDiscord id token (stats: SystemStats []) =

        let topStats = stats    |> Array.filter (fun s -> s.level = IronSde.SecurityLevel.Lowsec.ToString() ) 
                                |> Array.sortByDescending (fun s -> s.npcKills) 
                                |> Array.take 10
        
        let embeds = topStats |> Seq.map (fun s ->  let eb = (new Discord.EmbedBuilder())
                                                                .WithTitle(sprintf "%s - %s (%s) " s.regionName s.name s.level )
                                                    eb.Fields.Add((new EmbedFieldBuilder()).WithName("rats").WithValue(s.npcKills).WithIsInline(true))
                                                    eb.Fields.Add((new EmbedFieldBuilder()).WithName("ships / pods").WithValue(sprintf "%i / %i" s.shipKills s.podKills).WithIsInline(true))
                                                    eb.Fields.Add((new EmbedFieldBuilder()).WithName("jumps").WithValue(s.jumps).WithIsInline(true))
                                                    eb.Build()
                                                    )
                                |> Array.ofSeq
        let title = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") |> sprintf "Top 10 lowsec systems by ratting. For the last hour to %sZ" 
        
        let client = new Discord.Webhook.DiscordWebhookClient(id, token)
        
        let t = client.SendMessageAsync(title, false, embeds)

        t.Wait()
        

    [<EntryPoint>]
    let main argv = 
        try
            let id, token, output = match argv with
                                        | [| id; token; output |] ->    id, token, Some output
                                        | [| id; token; |] ->           id, token, None
                                        |  _ ->                         failwith "Missing discord webhook ID & token"
            
            let kills = getKillsJson() |> Async.RunSynchronously |> getKillsStats
            let jumpsJson = getJumpsJson() |> Async.RunSynchronously
            let stats = joinJumps jumpsJson kills
            
            sendToDiscord (System.UInt64.Parse id) token stats
                        
            if output.IsSome then
                writeCsv output.Value stats            
            0

        with
        | ex -> System.Console.Error.WriteLine(ex.Message)
                2

