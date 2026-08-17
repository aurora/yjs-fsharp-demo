module Server.Program

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    app.Urls.Add("http://localhost:5251")

    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore
    app.UseWebSockets() |> ignore

    // Everything collaboration-related happens on ws(s)://.../ws?room=<id>.
    // Anything else falls through to static file serving (the Fable-built client).
    app.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next ->
        task {
            if ctx.Request.Path.Equals(PathString("/ws")) then
                if ctx.WebSockets.IsWebSocketRequest then
                    let roomId =
                        match ctx.Request.Query.TryGetValue("room") with
                        | true, v when v.Count > 0 && not (String.IsNullOrWhiteSpace v.[0]) -> v.[0]
                        | _ -> "default"
                    let! socket = ctx.WebSockets.AcceptWebSocketAsync()
                    do! Server.Rooms.handleConnection roomId socket
                else
                    ctx.Response.StatusCode <- 400
            else
                do! next.Invoke(ctx)
        }
        :> Task))
    |> ignore

    Server.Rooms.log ConsoleColor.White "================================================================"
    Server.Rooms.log ConsoleColor.White " Yjs + F# collaboration demo server"
    Server.Rooms.log ConsoleColor.White "   Web UI:    http://localhost:5251"
    Server.Rooms.log ConsoleColor.White "   WebSocket: ws://localhost:5251/ws?room=<id>"
    Server.Rooms.log ConsoleColor.White "================================================================"

    app.Run()
    0
