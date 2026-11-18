// Toasts de GAS-Admin.
// Uso desde JS: GasToast.success("..."), .error, .warning, .info
// Los toasts del servidor llegan en <script id="gas-toasts-data">.
(function () {
    "use strict";

    const MAX_VISIBLE = 4;

    const TYPES = {
        success: {
            title: "Éxito",
            duration: 4000,
            role: "status",
            icon: '<path d="M20 6 9 17l-5-5"/>'
        },
        error: {
            title: "Error",
            duration: 7000,
            role: "alert",
            icon: '<circle cx="12" cy="12" r="9"/><path d="m15 9-6 6M9 9l6 6"/>'
        },
        warning: {
            title: "Advertencia",
            duration: 6000,
            role: "alert",
            icon: '<path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"/><path d="M12 9v4M12 17h.01"/>'
        },
        info: {
            title: "Nota",
            duration: 5000,
            role: "status",
            icon: '<circle cx="12" cy="12" r="9"/><path d="M12 16v-4M12 8h.01"/>'
        }
    };

    const CLOSE_ICON = '<path d="M18 6 6 18M6 6l12 12"/>';

    function svg(paths) {
        return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + paths + "</svg>";
    }

    function container() {
        let el = document.getElementById("gas-toasts");
        if (!el) {
            el = document.createElement("div");
            el.id = "gas-toasts";
            el.className = "gas-toasts";
            el.setAttribute("aria-live", "polite");
            document.body.appendChild(el);
        }
        return el;
    }

    function dismiss(toast) {
        if (toast.classList.contains("is-leaving")) return;
        clearTimeout(toast._timer);
        toast.classList.add("is-leaving");

        const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        if (reduced) {
            toast.remove();
        } else {
            toast.addEventListener("animationend", () => toast.remove(), { once: true });
            // Respaldo por si la animación no se ejecuta.
            setTimeout(() => toast.remove(), 400);
        }
    }

    function show(type, message, options) {
        const config = TYPES[type] || TYPES.info;
        const duration = (options && options.duration) || config.duration;
        const title = (options && options.title) || config.title;

        const toast = document.createElement("div");
        toast.className = "gas-toast gas-toast--" + (TYPES[type] ? type : "info");
        toast.setAttribute("role", config.role);
        toast.style.setProperty("--duration", duration + "ms");
        toast.innerHTML =
            '<div class="gas-toast__icon">' + svg(config.icon) + "</div>" +
            '<div class="gas-toast__body">' +
            '<p class="gas-toast__title"></p>' +
            '<p class="gas-toast__message"></p>' +
            "</div>" +
            '<button type="button" class="gas-toast__close" aria-label="Cerrar notificación">' +
            svg(CLOSE_ICON) + "</button>" +
            '<div class="gas-toast__progress"></div>';

        // textContent evita inyectar HTML desde el mensaje.
        toast.querySelector(".gas-toast__title").textContent = title;
        toast.querySelector(".gas-toast__message").textContent = message;
        toast.querySelector(".gas-toast__close").addEventListener("click", () => dismiss(toast));

        // El tiempo se pausa mientras el cursor o el foco están sobre el toast.
        let remaining = duration;
        let startedAt = 0;
        const start = () => {
            startedAt = Date.now();
            toast._timer = setTimeout(() => dismiss(toast), remaining);
        };
        const pause = () => {
            clearTimeout(toast._timer);
            remaining -= Date.now() - startedAt;
        };
        toast.addEventListener("mouseenter", pause);
        toast.addEventListener("mouseleave", start);
        toast.addEventListener("focusin", pause);
        toast.addEventListener("focusout", start);

        const host = container();
        host.appendChild(toast);

        const visible = host.querySelectorAll(".gas-toast:not(.is-leaving)");
        if (visible.length > MAX_VISIBLE) {
            dismiss(visible[0]);
        }

        start();
        return toast;
    }

    window.GasToast = {
        show: show,
        success: (message, options) => show("success", message, options),
        error: (message, options) => show("error", message, options),
        warning: (message, options) => show("warning", message, options),
        info: (message, options) => show("info", message, options)
    };

    function showServerToasts() {
        const data = document.getElementById("gas-toasts-data");
        if (!data) return;
        try {
            JSON.parse(data.textContent || "[]")
                .forEach(t => show(t.type, t.message));
        } catch (e) {
            console.error("No se pudieron leer los toasts del servidor.", e);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", showServerToasts);
    } else {
        showServerToasts();
    }
})();
