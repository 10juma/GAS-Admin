// Editor de cotizaciones: conceptos, totales en vivo y envío ordenado de las líneas.
(function () {
    "use strict";

    const r2 = n => Math.round((n + Number.EPSILON) * 100) / 100;
    const num = v => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };
    const fmt = (n, moneda) => moneda + " " + window.GasFormato.numero(n);

    function editor() {
        return document.querySelector("#gas-modal-content form[data-cotizacion]");
    }

    function catalogo(form) {
        try { return JSON.parse(form.querySelector("[data-catalogo]")?.textContent || "[]"); }
        catch { return []; }
    }

    function recalcular(form) {
        if (!form) return;
        const moneda = form.querySelector("[data-moneda]")?.value || "MXN";
        const tasa = num(form.querySelector("[data-iva]")?.value) / 100;
        let bruto = 0, subtotal = 0, costo = 0, hayCosto = false;

        const filas = form.querySelectorAll("[data-linea]");
        filas.forEach(fila => {
            const v = campo => fila.querySelector(`[data-campo="${campo}"]`)?.value;
            const lineaBruta = r2(num(v("Cantidad")) * num(v("PrecioUnitario")));
            const importe = r2(lineaBruta * (1 - num(v("DescuentoPorcentaje")) / 100));
            bruto += lineaBruta;
            subtotal += importe;
            if (v("CostoUnitario") !== "" && v("CostoUnitario") !== undefined) {
                costo += r2(num(v("Cantidad")) * num(v("CostoUnitario")));
                hayCosto = true;
            }
            const celda = fila.querySelector("[data-importe]");
            if (celda) celda.textContent = window.GasFormato.numero(importe);
        });

        subtotal = r2(subtotal);
        const iva = r2(subtotal * tasa);
        const set = (clave, texto) => { const el = form.closest(".modal-content").querySelector(`[data-total="${clave}"]`); if (el) el.innerHTML = texto; };
        set("subtotal", fmt(subtotal, moneda));
        set("descuento", bruto - subtotal > 0 ? "−" + fmt(r2(bruto - subtotal), moneda) : "—");
        set("iva", fmt(iva, moneda));
        set("total", fmt(r2(subtotal + iva), moneda));
        set("margen", window.GasMargen(hayCosto && subtotal > 0 ? (subtotal - costo) / subtotal : null));

        const vacio = form.closest(".modal-content").querySelector("[data-lineas-vacio]");
        if (vacio) vacio.classList.toggle("d-none", filas.length > 0);
    }

    function agregarLinea(form, datos) {
        const plantilla = form.closest(".modal-content").querySelector("template[data-linea-template]");
        const cuerpo = form.querySelector("[data-lineas]");
        if (!plantilla || !cuerpo) return;
        const fila = plantilla.content.firstElementChild.cloneNode(true);
        Object.entries(datos || {}).forEach(([campo, valor]) => {
            const el = fila.querySelector(`[data-campo="${campo}"]`);
            if (el && valor !== null && valor !== undefined) el.value = valor;
        });
        cuerpo.appendChild(fila);
        recalcular(form);
        fila.querySelector('[data-campo="Concepto"]')?.focus();
    }

    // Agregar del catálogo
    document.addEventListener("change", e => {
        const select = e.target;
        if (!(select instanceof HTMLSelectElement)) return;
        const form = editor();
        if (!form) return;

        if (select.hasAttribute("data-catalogo-select")) {
            const item = catalogo(form).find(x => String(x.id) === select.value);
            select.value = "";
            if (!item) return;
            const moneda = form.querySelector("[data-moneda]")?.value;
            if (item.precio !== null && item.moneda !== moneda) {
                window.GasToast && GasToast.warning(`${item.nombre} tiene precio en ${item.moneda}; revisa el precio en ${moneda}.`);
            }
            agregarLinea(form, {
                ServicioComercialId: item.id,
                Concepto: item.nombre,
                Descripcion: item.descripcion || "",
                Cantidad: 1,
                Unidad: item.unidad || "",
                PrecioUnitario: item.precio ?? 0,
                DescuentoPorcentaje: 0,
                CostoUnitario: item.costo ?? ""
            });
            return;
        }

        // Elegir oportunidad selecciona su cliente.
        if (select.hasAttribute("data-oportunidad")) {
            const cliente = select.selectedOptions[0]?.dataset.cliente;
            const selCliente = form.querySelector("[data-cliente]");
            if (cliente && selCliente) selCliente.value = cliente;
        }

        if (select.hasAttribute("data-condicion")) {
            form.querySelector("[data-anticipo]")?.classList.toggle("d-none", select.value !== "AnticipoYFinal");
        }

        if (select.hasAttribute("data-moneda")) recalcular(form);
    });

    document.addEventListener("click", e => {
        const form = editor();
        if (!form) return;
        if (e.target.closest("[data-agregar-libre]")) {
            agregarLinea(form, { Cantidad: 1, PrecioUnitario: 0, DescuentoPorcentaje: 0 });
        }
        const quitar = e.target.closest("[data-quitar-linea]");
        if (quitar) {
            quitar.closest("[data-linea]").remove();
            recalcular(form);
        }
    });

    document.addEventListener("input", e => {
        const form = e.target.closest && e.target.closest("form[data-cotizacion]");
        if (form) recalcular(form);
    });

    // Antes de enviar: nombrar los campos de cada línea en orden (Input.Lineas[i].Campo).
    document.addEventListener("submit", e => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-cotizacion")) return;
        form.querySelectorAll("[data-linea]").forEach((fila, i) => {
            fila.querySelectorAll("[data-campo]").forEach(el => {
                el.name = `Input.Lineas[${i}].${el.dataset.campo}`;
            });
        });
    }, true);

    document.addEventListener("gas:modal-cargado", () => recalcular(editor()));
    recalcular(editor());
})();
