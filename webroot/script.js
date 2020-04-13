//const ws = new WebSocket("ws://ubuntu:9865/websocketurl", ["Eigenes", "Zweites"])
const ws = new WebSocket("ws://ubuntu:9865/websocketurl")


let i = 0

ws.onclose = () => console.log("Closed")
//ws.onmessage = p => console.log(p.data)
ws.onmessage = p => { 
    if (++i == 1_000_000)
        alert("Fertig" + i)
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


