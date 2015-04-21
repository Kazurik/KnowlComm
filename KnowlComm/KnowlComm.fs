namespace KnowlComm
open System.Net.Sockets
open System.IO
open System
open System.Net.Security;
open System.Security.Authentication;
open System.Security.Cryptography.X509Certificates;

type KnowlVersion = 
    | I1
    | I2

type ChatMessage = {ChatName:string; Sender:string; Message:string}

type KnowlConnector (version, address, port) = 
    let VerifyServerCertificate sender (certificate:X509Certificate) chain sslPolicyErrors =
        let chain2 = new X509Chain()
        if not (System.IO.File.Exists("root.der")) then failwith "Missing root.der"
        chain2.ChainPolicy.ExtraStore.Add(new X509Certificate2("root.der")) |> ignore
        chain2.ChainPolicy.RevocationMode <- X509RevocationMode.NoCheck
        chain2.Build(new X509Certificate2(certificate)) |> ignore
        if chain2.ChainStatus.Length = 0 then true
        else 
            let status = chain2.ChainStatus.[0].Status
            status = X509ChainStatusFlags.NoError || status = X509ChainStatusFlags.UntrustedRoot
    let SelectCertificate sender targetHost (certs:X509CertificateCollection) remote issuers = certs.[0]
    let client = new TcpClient(address, port)
    let stream =
        match version with
        | I1 -> client.GetStream() :> Stream
        | I2 -> let stream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback (VerifyServerCertificate), new LocalCertificateSelectionCallback(SelectCertificate))
                let certs = new X509Certificate2Collection();
                if not (System.IO.File.Exists("client.p12")) then failwith "Missing client.p12"
                certs.Import("client.p12");
                stream.AuthenticateAsClient(address, certs, SslProtocols.Tls12, false)
                stream :> Stream
    let reader = new BinaryReader(stream)

    let eRegister = Event<_>()
    let eMessageReceived = Event<ChatMessage>()
    let eConnectionLost = Event<_>()

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
        while client.Connected do 
            match reader.ReadByte() with
            | 1uy -> eRegister.Trigger()
            | 2uy -> eMessageReceived.Trigger(ReadMessage())
            | _ -> failwith "Unknown message received!"
        eConnectionLost.Trigger()
        }

    let StartListener () =
        match version with
        | I1 -> Async.Start HandleMessagesI1
        | I2 -> Async.Start HandleMessagesI1 //Uses same as I1.

    //Connect
    do
        StartListener()
        match version with
        | I1 | I2 -> stream.WriteByte(1uy)

    interface IDisposable with
        member x.Dispose() = 
            stream.Close()
            client.Close()

    member x.Register = eRegister.Publish
    member x.MessageRecevied = eMessageReceived.Publish
    member x.ConnectionLost = eConnectionLost.Publish

    member x.SendMessage (chatName:string) (message:string) =
        let payload = Array.concat ([[|byte 2; byte chatName.Length|]; 
                                    StrToBytes chatName;  IntToBytes message.Length; 
                                    StrToBytes message])
        stream.Write(payload, 0, payload.Length)