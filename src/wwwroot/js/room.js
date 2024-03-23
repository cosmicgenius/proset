const room_id = document.getElementById("room_id").textContent;
let source;
let game_state;
let player_state;

let card_active = [];

document.addEventListener("keydown", event => {
    for (let i = 1; i <= Math.min(9, game_state.num_cards); i++) {
        if (event.key === `${i}`) {
            document.getElementById(`card-${i}`).click();
        }
    }

    if (game_state.num_cards >= 10 && event.key === "0") {
        document.getElementById(`card-10`).click();
    }
}); 

function toggleCard(idx) {
    console.log(`Toggle card: ${idx}`);
    card_active[idx] = !card_active[idx];
    document.getElementById(`card-${idx + 1}`).className = 
        card_active[idx] ? "card-active" : "card-inactive";
}

function isValid() {
    return game_state.current_cards.reduce(
        (acc, cur, idx) => card_active[idx] ? acc ^ cur : acc, 0
    ) === 0 && card_active.some(b => b);
}

function generateCardInside(card) {
    return Array.from({ length: game_state.num_tokens }, (_, idx) =>
        `<div class="dot dot-${idx + 1} dot-${(card & (1 << idx)) > 0 ? "active" : "inactive"}"></div>`
    ).join("");
}

function handleSSEEvent(data) {
    // game state update
    if (data.event_type === 1) {
        game_state = data;

        if (game_state.current_cards.length !== 0) {
            document.getElementById("cards").innerHTML = 
                game_state.current_cards.map((card, idx) => `
                        <div 
                            class="card-inactive"
                            name="card-${idx + 1}" 
                            id="card-${idx + 1}"
                            onclick="toggleCard(${idx})"
                        >
                            ${generateCardInside(card)}
                        </div>
                    `).join("\n");

            card_active = game_state.current_cards.map(_ => false);
        } else {
            document.getElementById("cards").innerHTML = 
                `Game over! <button onclick="newGame()">New Game</button>`;
        }
    }
    // player state update
    else if (data.event_type === 2) {
        player_state = data;
        document.getElementById("players").innerHTML = 
            player_state.players.map((player, idx) => `
                    <li>${player}: ${player_state.scores[idx]}</li>
                `).join("\n");
    }
}

function bindSSE(url) {
    if (source) source.close();
    source = new EventSource(url);

    source.onmessage = event => {
        console.log("Message: " + event.data);
        handleSSEEvent(JSON.parse(event.data));
    };
    source.onopen = _event => {
        console.log("Connected to SSE");
    };
    source.onerror = _event => {
        console.log("Error in SSE connection");
    };
}

window.onload = () => bindSSE(`/api/sse/${room_id}`);

function emit() {
    if (isValid()) {
        console.log("Found proset :)");
        fetch(`/api/found/${room_id}`, {
            method: "POST",
            body: JSON.stringify({
                cards: game_state.current_cards.filter((_, idx) => card_active[idx])
            }),
            headers: {
                "Content-type": "application/json; charset=UTF-8"
            }
        });
    } else {
        console.log("Not proset :(");
    }
}

function newGame() {
    fetch(`/api/new-game/${room_id}`, {
        method: "POST"
    });
}

