//const ws = new WebSocket("ws://localhost:9865/websocketurl", ["Eigenes", "Zweites"])
const ws = new WebSocket("ws://localhost:9865/websocketurl")

const sender = document.getElementById('sender')


let objs = Array.from(Array(10000).keys()).map(n => { 
    return {
        text: "Ein Objekt 😃",
        number: n
    }
})
let lang = JSON.stringify(objs)

sender.onclick = () => ws.send(lang)
//sender.onclick = () => ws.send("Das kommt aus dem schönen WebSocket! 😃😃😃")
