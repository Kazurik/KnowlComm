namespace KnowlComm
open System.Net.Sockets
open System.IO

type KnowlVersion = | I1

type ChatMessage = {ChatName:string; Sender:string; Message:string}

type KnowlConnector (version, address, port) = 
    let client = new TcpClient(address, port)
    let stream = client.GetStream()
    let reader = new BinaryReader(stream)

    let eRegister = Event<_>()
    let eMessageReceived = Event<ChatMessage>()

    let BToStr data len = System.Text.Encoding.ASCII.GetString(data, 0, len)

    let ReadStringB () =                                                                                      
         let len = int(reader.ReadByte())                                                                                
         let data = Array.create (len) 0uy                                                                               
         BToStr data (reader.Read(data, 0, len))                                                                         

    let ReadStringI () =                                                                                      
         let len = reader.ReadInt32()                                                                                    
         let data = Array.create (len) 0uy                                                                               
         BToStr data (reader.Read(data, 0, len))
    
    let ReadMessage () = {ChatName=ReadStringB(); Sender=ReadStringB(); Message=ReadStringI()}
    let StrToBytes (str:string) = System.Text.Encoding.UTF8.GetBytes(str)
    let IntToBytes (i:int) = if System.BitConverter.IsLittleEndian then System.BitConverter.GetBytes(i) else Array.rev (System.BitConverter.GetBytes(i))

    let HandleMessagesI1 = async{
        while true do 
            match reader.ReadByte() with
            | 1uy -> eRegister.Trigger()
            | 2uy -> eMessageReceived.Trigger(ReadMessage())
            | _ -> failwith "Unknown message received!"
        }

    let StartListener () =
        match version with
        | I1 -> Async.Start HandleMessagesI1

    //Connect
    do
        StartListener()
        match version with
        | I1 -> stream.WriteByte(1uy);

    member x.Register = eRegister.Publish
    member x.MessageRecevied = eMessageReceived.Publish
    member x.SendMessage (chatName:string) (message:string) =
        let payload = Array.concat ([[|byte 2; byte chatName.Length|]; 
                                    StrToBytes chatName;  IntToBytes message.Length; 
                                    StrToBytes message])
        stream.Write(payload, 0, payload.Length)