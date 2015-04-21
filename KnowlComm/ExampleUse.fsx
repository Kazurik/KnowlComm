// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.
#load "KnowlComm.fs"

open KnowlComm

printfn "Enter address"
let address = System.Console.ReadLine()
printfn "Got %s" address
let comm = new KnowlConnector(I2, address, 9696)
comm.Register.Add (fun () -> printfn "Connected")
comm.MessageRecevied.Add (fun m -> printfn "%s:%s" m.ChatName m.Message)
comm.SendMessage "#kazurik/$2be315d399d70eea" "butts"
comm.ConnectionLost.Add (fun _ -> printfn "Connection lost")