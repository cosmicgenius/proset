const room_id = document.getElementById("room_id").textContent;
let source;
let game_state;

let card_active = [];

document.addEventListener("keydown", event => {
    for (let i = 1; i <= Math.min(9, game_state.cards); i++) {
        if (event.key === `${i}`) {
            document.getElementById(`card-${i}`).click();
        }
    }

    if (game_state.cards >= 10 && event.key === "0") {
        document.getElementById(`card-10`).click();
    }
}); 

function toggleCard(idx) {
    console.log(`Toggle card: ${idx}`);
    card_active[idx] = !card_active[idx];
    document.getElementById(`card-${idx + 1}`).className = 
        card_active[idx] ? "card-active" : "card-inactive";
}

function bindSSE(url) {
    if (source) source.close();
    source = new EventSource(url);

    source.onmessage = event => {
        console.log("Message: " + event.data);
        game_state = JSON.parse(event.data);

        document.getElementById("cards").innerHTML = 
            game_state.current_cards.map((card, idx) => `
                    <div 
                        class="card-inactive"
                        name="card-${idx + 1}" 
                        id="card-${idx + 1}"
                        onclick="toggleCard(${idx})"
                    >
                        ${card}
                    </div>
                `).join("\n");

        card_active = game_state.current_cards.map(_ => false);
    };
    source.onopen = _event => {
        console.log("Connected to SSE");
    };
    source.onerror = _event => {
        console.log("Error in SSE connection");
    };
}

window.onload = () => bindSSE("/api/sse/" + room_id);

