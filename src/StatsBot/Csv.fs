namespace StatsBot

module Csv=
    
    let writeCsv(output: string) (stats: SystemStats []) =
        use writer = new System.IO.StreamWriter(output, false, System.Text.Encoding.UTF8)
        use csv = new CsvHelper.CsvWriter(writer)
            
        csv.WriteHeader(typeof<SystemStats>)
        csv.NextRecord()

        stats |> Seq.iter (fun s -> csv.WriteRecord(s)
                                    csv.NextRecord())
            

