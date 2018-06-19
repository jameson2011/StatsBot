#r @"packages/FAKE/tools/FakeLib.dll"
#r @"packages/FAKE.IO.FileSystem/lib/netstandard2.0/Fake.IO.FileSystem.dll"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO.Globbing.Operators
open Fake.IO
open Fake.DotNet


// Definitions
let buildDir = "build/"
let appProjects = "src/StatsBot.sln"

// Targets
Target.create "ScrubArtifacts" (fun _ -> Shell.cleanDirs [ buildDir; ] )

Target.create "BuildApp" (fun _ -> 
                            appProjects
                            |> MSBuild.build (fun opts ->
                                                            { opts with
                                                                RestorePackagesFlag = false
                                                                Targets = ["Rebuild"]
                                                                Verbosity = Some MSBuildVerbosity.Minimal
                                                                Properties =
                                                                  [ "VisualStudioVersion", "15.0"
                                                                    //"OutputPath", buildDir
                                                                    "Configuration", "Release"
                                                                  ]
                                                            })
                            )

Target.create "All" (fun _ -> Trace.trace "Built" )

// Dependencies

"ScrubArtifacts" 
==> "BuildApp"
==> "All"

Target.runOrDefault "All"
