module Types
open System.IO

type Opcode = 
    // Diese Nachricht muss an die vorherige angehängt werden. Wenn der fin-Wert 0 ist, folgen weitere Fragmente, 
    // bei fin=1 ist die Nachricht komplett verarbeitet.
    | ContinuationFrame = 0uy
    | Text = 1uy
    | Binary = 2uy
    | Close = 8uy
    // Ping erhalten, direkt einen Pong zurücksenden mit denselben payload-Daten
    | Ping = 9uy
    | Pong = 10uy

type Session = {
    Start: (Stream -> unit) -> (unit -> unit) -> (byte array -> Async<unit>) 
}