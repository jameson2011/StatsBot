namespace StatsBot

module Program=    
   
    [<EntryPoint>]
    let main argv = 
        try
            let id, token, output = match argv with
                                        | [| |] -> failwith "Command format: <channel ID> <Discord token> | optional: <path to CSV file>"
                                        | [| id; token; output |] ->    id, token, Some output
                                        | [| id; token; |] ->           id, token, None
                                        |  _ ->                         failwith "Missing discord webhook ID & token"
            
            let stats = Data.getData() |> Async.RunSynchronously
            
            stats |> StatsBot.Discord.sendToDiscord (System.UInt64.Parse id) token
                        
            if output.IsSome then
                stats |> Csv.writeCsv output.Value 
            0

        with
        | ex -> System.Console.Error.WriteLine(ex.Message)
                2

