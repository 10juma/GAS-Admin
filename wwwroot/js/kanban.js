// Kanban de oportunidades: arrastrar tarjetas entre etapas y reordenar.
(function () {
    "use strict";

    const tablero = document.querySelector("[data-kanban]");
    if (!tablero) return;

    const editable = tablero.dataset.editable === "1";
    const url = tablero.dataset.moverUrl;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    let arrastrando = null;
    let origen = null;
    let soltado = false;

    function actualizarConteos() {
        tablero.querySelectorAll(".gas-kanban__col").forEach(col => {
            const n = col.querySelectorAll(".gas-kanban__card").length;
            const conteo = col.querySelector("[data-conteo]");
            if (conteo && col.dataset.etapa !== "Ganada" && col.dataset.etapa !== "Perdida") conteo.textContent = n;
        });
    }

    function posicionDestino(lista, y) {
        const tarjetas = [...lista.querySelectorAll(".gas-kanban__card:not(.is-dragging)")];
        return tarjetas.find(t => {
            const r = t.getBoundingClientRect();
            return y < r.top + r.height / 2;
        }) || null;
    }

    if (editable) {
        tablero.addEventListener("dragstart", e => {
            const card = e.target.closest(".gas-kanban__card");
            if (!card) return;
            arrastrando = card;
            origen = { lista: card.parentElement, siguiente: card.nextElementSibling };
            soltado = false;
            card.classList.add("is-dragging");
            e.dataTransfer.effectAllowed = "move";
            e.dataTransfer.setData("text/plain", card.dataset.id);
        });

        tablero.addEventListener("dragend", () => {
            // Soltada fuera de una columna (o con Esc): regresa a donde estaba, porque no se guardó nada.
            if (arrastrando && !soltado && origen) origen.lista.insertBefore(arrastrando, origen.siguiente);
            if (arrastrando) arrastrando.classList.remove("is-dragging");
            tablero.querySelectorAll(".is-over").forEach(c => c.classList.remove("is-over"));
            arrastrando = null;
        });

        tablero.addEventListener("dragover", e => {
            const lista = e.target.closest("[data-lista]");
            if (!lista || !arrastrando) return;
            e.preventDefault();
            tablero.querySelectorAll(".is-over").forEach(c => c.classList.remove("is-over"));
            lista.closest(".gas-kanban__col").classList.add("is-over");
            const antes = posicionDestino(lista, e.clientY);
            const vacio = lista.querySelector(".gas-kanban__empty");
            lista.insertBefore(arrastrando, antes || vacio);
        });

        tablero.addEventListener("drop", async e => {
            const lista = e.target.closest("[data-lista]");
            if (!lista || !arrastrando) return;
            soltado = true;
            e.preventDefault();
            const card = arrastrando;
            const col = lista.closest(".gas-kanban__col");
            const etapa = col.dataset.etapa;
            const ids = [...lista.querySelectorAll(".gas-kanban__card")].map(c => parseInt(c.dataset.id, 10));
            actualizarConteos();
            card.classList.add("is-saving");

            try {
                const r = await fetch(url, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                    body: JSON.stringify({ id: parseInt(card.dataset.id, 10), etapa, ids })
                });
                if (!r.ok) throw new Error(r.status);
                const datos = await r.json();
                const prob = card.querySelector("[data-prob]");
                if (prob) prob.textContent = datos.probabilidad + "%";
                if (datos.requiereMotivo && window.GasModal) {
                    GasToast.warning("Registra el motivo por el que se perdió.");
                    GasModal.abrir(datos.editarUrl);
                }
            } catch {
                // Regresar la tarjeta a su lugar.
                origen.lista.insertBefore(card, origen.siguiente);
                actualizarConteos();
                window.GasToast && GasToast.error("No se pudo mover la oportunidad. Inténtalo de nuevo.");
            } finally {
                card.classList.remove("is-saving");
            }
        });
    }

    // Abrir con Enter desde el teclado.
    tablero.addEventListener("keydown", e => {
        const card = e.target.closest(".gas-kanban__card");
        if (card && e.key === "Enter") card.click();
    });
})();
