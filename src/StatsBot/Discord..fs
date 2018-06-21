namespace StatsBot

module Discord=
    
    open Discord

    let dotlanSystemUri(systemName: string) =
        systemName.Replace(" ", "_")
            |> sprintf "http://evemaps.dotlan.net/map/Genesis/%s#npc24"

    let sendToDiscord id token (stats: SystemStats []) =

        let topStats = stats    |> Array.filter (fun s -> s.level = IronSde.SecurityLevel.Lowsec.ToString() ) 
                                |> Array.sortByDescending (fun s -> s.npcKills) 
                                |> Array.take 10
        
        let embeds = topStats |> Seq.map (fun s ->  let eb = (new Discord.EmbedBuilder())
                                                                .WithTitle(sprintf "%s - %s (%s) " s.regionName s.name s.level )
                                                                .WithUrl(dotlanSystemUri s.name)
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

