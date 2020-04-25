//const ws = new WebSocket("ws://ubuntu:9865/websocketurl", ["Eigenes", "Zweites"])
const ws = new WebSocket("ws://frisco:9865/websocketurl")

ws.onopen = () => ws.send("WS Opened")
let i = 0

ws.onclose = () => console.log("Closed")
ws.onmessage = p => { 
    if (++i == 1_000_000) {
        alert("Fertig" + i)
        console.log(JSON.parse(p.data))
        i = 0
    }
}

const sender = document.getElementById('sender')


let objs = Array.from(Array(100000).keys()).map(n => { 
    return {
        text: "Ein Objekt 😃",
        number: n
    }
})
let lang = JSON.stringify(objs)

//sender.onclick = () => ws.send(lang)
sender.onclick = () => ws.send("Das kommt aus dem schönen WebSocket! 😃😃😃")


