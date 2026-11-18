// GAS-Admin: menú lateral, envío seguro de formularios, confirmaciones y copiar.
(function () {
    "use strict";

    const ICONO_ALERTA =
        '<svg class="gas-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ' +
        'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"/>' +
        '<path d="M12 9v4M12 17h.01"/></svg>';

    /* ---------- Menú lateral en pantallas pequeñas ---------- */
    const body = document.body;
    const abrirMenu = () => body.classList.add("sidebar-open");
    const cerrarMenu = () => body.classList.remove("sidebar-open");
    document.querySelectorAll("[data-sidebar-open]").forEach(b => b.addEventListener("click", abrirMenu));
    document.querySelectorAll("[data-sidebar-close]").forEach(b => b.addEventListener("click", cerrarMenu));
    document.addEventListener("keydown", e => { if (e.key === "Escape") cerrarMenu(); });

    /* ---------- Indicador de carga al cambiar de página ---------- */
    // Barra arriba, spinner en la opción del menú y contenido atenuado mientras llega la página.
    const barra = document.createElement("div");
    barra.className = "gas-carga-barra";
    barra.setAttribute("aria-hidden", "true");
    body.appendChild(barra);
    let esperaCarga = null;
    let limiteCarga = null;

    function terminarCarga() {
        clearTimeout(esperaCarga);
        clearTimeout(limiteCarga);
        document.documentElement.classList.remove("gas-navegando");
        document.querySelectorAll(".gas-nav__item.is-cargando").forEach(a => a.classList.remove("is-cargando"));
    }

    function iniciarCarga(enlace) {
        terminarCarga();
        if (enlace && enlace.classList.contains("gas-nav__item")) {
            enlace.classList.add("is-cargando");
            cerrarMenu();
        }
        // Pequeña espera: si la página llega al instante no parpadea nada.
        esperaCarga = setTimeout(() => document.documentElement.classList.add("gas-navegando"), 120);
        limiteCarga = setTimeout(terminarCarga, 20000);
    }

    window.addEventListener("pageshow", terminarCarga); // al volver con "Atrás"

    // En window: corre después de los demás manejadores, así respeta los que cancelan el clic.
    window.addEventListener("click", e => {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        const a = e.target instanceof Element ? e.target.closest("a[href]") : null;
        if (!a || a.target || (a.getAttribute("href") || "").startsWith("#") || a.hasAttribute("download") || a.hasAttribute("data-modal-url") || a.hasAttribute("data-bs-toggle")) return;
        const destino = new URL(a.href, location.href);
        if (destino.origin !== location.origin || destino.searchParams.has("handler")) return; // descargas y PDF
        if (destino.pathname === location.pathname && destino.search === location.search && destino.hash) return;
        iniciarCarga(a);
    });

    // Filtros y búsquedas (formularios GET que cambian de página).
    window.addEventListener("submit", e => {
        const form = e.target;
        if (e.defaultPrevented || !(form instanceof HTMLFormElement) || form.target) return;
        if ((form.getAttribute("method") || "get").toLowerCase() === "get") iniciarCarga(null);
    });

    /* ---------- Secciones del menú: solo una abierta a la vez ---------- */
    const raiz = document.documentElement;
    const esMini = () => raiz.classList.contains("gas-mini") && window.matchMedia("(min-width: 992px)").matches;
    const grupos = Array.from(document.querySelectorAll(".gas-nav__group"));
    grupos.forEach(grupo => {
        const cabeza = grupo.querySelector(".gas-nav__head");
        cabeza.addEventListener("click", () => {
            if (esMini()) return; // contraído: la sección se ve flotando al pasar el mouse
            const abrir = !grupo.classList.contains("is-open");
            grupos.forEach(g => {
                g.classList.toggle("is-open", g === grupo && abrir);
                g.querySelector(".gas-nav__head").setAttribute("aria-expanded", g === grupo && abrir ? "true" : "false");
            });
        });
        // Contraído: la sección flotante se alinea con su ícono sin salirse de la pantalla.
        const ubicar = () => {
            if (!esMini()) return;
            const flotante = grupo.querySelector(".gas-nav__items");
            const alto = flotante.querySelector(".gas-nav__inner").scrollHeight || 200;
            const arriba = Math.max(8, Math.min(cabeza.getBoundingClientRect().top - 6, window.innerHeight - alto - 12));
            flotante.style.setProperty("--gas-fly-top", arriba + "px");
        };
        grupo.addEventListener("mouseenter", ubicar);
        grupo.addEventListener("focusin", ubicar);
    });

    /* ---------- Contraer el menú a solo íconos (se recuerda en este navegador) ---------- */
    document.querySelectorAll("[data-menu-mini]").forEach(boton => {
        const rotular = () => {
            const texto = raiz.classList.contains("gas-mini") ? "Expandir menú" : "Contraer menú";
            boton.setAttribute("aria-label", texto);
            boton.title = texto;
        };
        rotular();
        boton.addEventListener("click", () => {
            const mini = raiz.classList.toggle("gas-mini");
            try { localStorage.setItem("gas.menu.mini", mini ? "1" : "0"); } catch (e) { /* sin almacenamiento */ }
            rotular();
        });
    });

    /* ---------- Spinner en botones ---------- */
    function marcarCargando(boton) {
        if (!boton || boton.classList.contains("is-loading")) return;
        boton.classList.add("is-loading");
        boton.setAttribute("aria-busy", "true");
        const spinner = document.createElement("span");
        spinner.className = "gas-spinner";
        spinner.setAttribute("aria-hidden", "true");
        boton.prepend(spinner);
    }

    function limpiarCargando() {
        document.querySelectorAll(".is-loading").forEach(b => {
            b.classList.remove("is-loading");
            b.removeAttribute("aria-busy");
            b.querySelectorAll(".gas-spinner").forEach(s => s.remove());
            if (b.dataset.gasDisabled) {
                b.disabled = false;
                delete b.dataset.gasDisabled;
            }
        });
        document.querySelectorAll("form[data-enviando]").forEach(f => delete f.dataset.enviando);
    }

    // Al volver con "Atrás" el navegador puede restaurar la página tal cual.
    window.addEventListener("pageshow", e => { if (e.persisted) limpiarCargando(); });

    /* ---------- Diálogo de confirmación ---------- */
    function confirmar({ titulo, mensaje, ok, tono }) {
        if (typeof HTMLDialogElement !== "function") {
            return Promise.resolve(window.confirm(mensaje));
        }

        return new Promise(resolve => {
            const dialog = document.createElement("dialog");
            dialog.className = "gas-dialog" + (tono === "danger" ? " gas-dialog--danger" : "");
            dialog.innerHTML =
                '<div class="gas-dialog__body">' +
                '<div class="gas-dialog__icon">' + ICONO_ALERTA + "</div>" +
                '<div><h2 class="gas-dialog__title"></h2><p class="gas-dialog__message"></p></div>' +
                "</div>" +
                '<div class="gas-dialog__actions">' +
                '<button type="button" class="btn btn-gas-outline" data-cancelar>Cancelar</button>' +
                '<button type="button" class="btn ' +
                (tono === "danger" ? "btn-gas-danger-solid" : "btn-gas") + '" data-ok></button>' +
                "</div>";

            dialog.querySelector(".gas-dialog__title").textContent = titulo || "¿Confirmas esta acción?";
            dialog.querySelector(".gas-dialog__message").textContent = mensaje;
            dialog.querySelector("[data-ok]").textContent = ok || "Confirmar";

            // Se resuelve con los botones (no con el evento "close", que no siempre
            // llega cuando el diálogo vive dentro de un modal de Bootstrap).
            let resuelto = false;
            const terminar = valor => {
                if (resuelto) return;
                resuelto = true;
                if (dialog.open) dialog.close();
                dialog.remove();
                resolve(valor);
            };

            dialog.querySelector("[data-ok]").addEventListener("click", () => terminar(true));
            dialog.querySelector("[data-cancelar]").addEventListener("click", () => terminar(false));
            dialog.addEventListener("cancel", e => { e.preventDefault(); terminar(false); });
            dialog.addEventListener("keydown", e => {
                if (e.key === "Escape") {
                    e.preventDefault();
                    e.stopPropagation();
                    terminar(false);
                }
            });
            dialog.addEventListener("click", e => { if (e.target === dialog) terminar(false); });

            // Dentro del modal abierto para no pelear con su control de foco.
            (document.querySelector(".modal.show") || document.body).appendChild(dialog);
            dialog.showModal();
            dialog.querySelector("[data-cancelar]").focus();
        });
    }

    window.GasConfirm = confirmar;

    /* ---------- Envío de formularios: confirmación + sin doble clic ---------- */
    document.addEventListener("submit", async e => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || form.method === "dialog") return;

        if (form.dataset.enviando) {
            e.preventDefault();
            return;
        }

        const mensaje = form.getAttribute("data-confirm");
        if (mensaje && !form.dataset.confirmado) {
            e.preventDefault();
            const submitter = e.submitter;
            const aceptado = await confirmar({
                titulo: form.getAttribute("data-confirm-title"),
                mensaje,
                ok: form.getAttribute("data-confirm-ok"),
                tono: form.getAttribute("data-confirm-tone")
            });
            if (aceptado) {
                form.dataset.confirmado = "1";
                form.requestSubmit(submitter && submitter.form === form ? submitter : undefined);
                delete form.dataset.confirmado;
            }
            return;
        }

        // Formularios que abren otra pestaña (target="_blank"): la página actual no cambia,
        // así que solo se bloquea un momento para evitar doble clic.
        if (form.hasAttribute("data-sin-espera")) {
            form.dataset.enviando = "1";
            setTimeout(() => delete form.dataset.enviando, 2000);
            return;
        }

        form.dataset.enviando = "1";
        const boton = e.submitter || form.querySelector('[type="submit"]');
        marcarCargando(boton);

        // Se deshabilitan después del envío para no perder el valor del botón.
        setTimeout(() => {
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(b => {
                if (!b.disabled) {
                    b.disabled = true;
                    b.dataset.gasDisabled = "1";
                }
            });
        }, 0);
    });

    /* ---------- Enlaces con aspecto de botón ---------- */
    document.addEventListener("click", e => {
        const link = e.target.closest("a.btn[href]");
        if (!link || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        const href = link.getAttribute("href");
        if (!href || href.startsWith("#") || link.target === "_blank") return;
        if (link.classList.contains("is-loading")) {
            e.preventDefault();
            return;
        }
        marcarCargando(link);
    });

    /* ---------- Copiar al portapapeles: <button data-copy="#id"> ---------- */
    document.addEventListener("click", async e => {
        const btn = e.target.closest("[data-copy]");
        if (!btn) return;
        const target = document.querySelector(btn.getAttribute("data-copy"));
        if (!target) return;
        try {
            await navigator.clipboard.writeText(target.textContent.trim());
            window.GasToast && GasToast.success("Copiado al portapapeles.");
        } catch {
            window.GasToast && GasToast.warning("No se pudo copiar. Selecciona el texto y cópialo manualmente.");
        }
    });

    /* ---------- Permisos: "Gestionar" implica "Ver" (matriz de roles) ---------- */
    document.addEventListener("change", e => {
        const cb = e.target;
        if (!(cb instanceof HTMLInputElement) || cb.name !== "Seleccionados") return;
        const fila = cb.closest("tr");
        const ver = fila && fila.querySelector('[data-tipo="Ver"]');
        const gestionar = fila && fila.querySelector('[data-tipo="Gestionar"]');
        if (cb === gestionar && cb.checked && ver) ver.checked = true;
        if (cb === ver && !cb.checked && gestionar) gestionar.checked = false;
        const contador = document.getElementById("permisos-count");
        if (contador) {
            contador.textContent = cb.form.querySelectorAll('input[name="Seleccionados"]:checked').length;
        }
    });

    /* ---------- Resaltar la opción cuando su select tiene valor ---------- */
    document.addEventListener("change", e => {
        const select = e.target;
        if (!(select instanceof HTMLSelectElement) || !select.hasAttribute("data-toggle-on")) return;
        const opcion = select.closest(".gas-option");
        if (opcion) opcion.classList.toggle("is-on", select.value !== "");
    });

    /* ---------- Formato de dinero y margen en vivo (catálogo comercial) ---------- */
    const formatoNumero = new Intl.NumberFormat("es-MX", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    window.GasFormato = { numero: n => formatoNumero.format(n || 0) };

    function chipMargen(margen) {
        if (margen === null) return '<span class="text-muted">—</span>';
        const clase = margen >= 0.4 ? "gas-badge--success" : margen >= 0.2 ? "gas-badge--warning" : "gas-badge--danger";
        return '<span class="gas-badge ' + clase + '">' + (margen * 100).toFixed(1).replace(/\.0$/, "") + "%</span>";
    }
    window.GasMargen = chipMargen;

    function actualizarMargen(form) {
        const salida = form.querySelector("[data-margen-salida]");
        if (!salida) return;
        const precio = parseFloat(form.querySelector("[data-precio]")?.value);
        const costo = parseFloat(form.querySelector("[data-costo]")?.value);
        salida.innerHTML = precio > 0 && !isNaN(costo) ? chipMargen((precio - costo) / precio) : chipMargen(null);
    }

    document.addEventListener("input", e => {
        const form = e.target.closest && e.target.closest("form[data-margen]");
        if (form) actualizarMargen(form);
    });

    // Sugerir la unidad al elegir modalidad si está vacía.
    document.addEventListener("change", e => {
        const select = e.target;
        if (!(select instanceof HTMLSelectElement) || select.name !== "Input.Modalidad") return;
        const unidad = select.form && select.form.querySelector('[name="Input.Unidad"]');
        const sugerida = select.selectedOptions[0] && select.selectedOptions[0].dataset.unidad;
        if (unidad && !unidad.value && sugerida) unidad.value = sugerida;
    });

    // Avisar a los scripts de cada página cuando se carga el contenido de un modal.
    const contenidoModal = () => document.getElementById("gas-modal-content");
    function modalCargado() {
        const c = contenidoModal();
        if (!c) return;
        c.querySelectorAll("form[data-margen]").forEach(actualizarMargen);
        document.dispatchEvent(new CustomEvent("gas:modal-cargado", { detail: c }));
    }
    window.GasModalCargado = modalCargado;

    /* ---------- Oportunidad: etapa sugiere probabilidad y muestra motivo de pérdida ---------- */
    document.addEventListener("change", e => {
        const select = e.target;
        if (!(select instanceof HTMLSelectElement) || !select.hasAttribute("data-etapa")) return;
        const form = select.form;
        const prob = form.querySelector("[data-prob-input]");
        const sugerida = select.selectedOptions[0] && select.selectedOptions[0].dataset.prob;
        if (prob && sugerida !== undefined) prob.value = sugerida;
        const motivo = form.querySelector("[data-motivo]");
        if (motivo) motivo.classList.toggle("d-none", select.value !== "Perdida");
    });

    /* ---------- Persona: puestos del departamento y datos desde el usuario ---------- */
    function filtrarPuestos(form) {
        const depto = form.querySelector("[data-departamento]");
        const puesto = form.querySelector("[data-puesto]");
        if (!depto || !puesto) return;
        [...puesto.options].forEach(o => {
            if (!o.value) return;
            const visible = !depto.value || o.dataset.depto === depto.value;
            o.hidden = !visible;
            o.disabled = !visible;
        });
        if (puesto.selectedOptions[0]?.disabled) puesto.value = "";
    }

    document.addEventListener("change", e => {
        const el = e.target;
        if (!(el instanceof HTMLSelectElement) || !el.form) return;
        if (el.hasAttribute("data-departamento")) filtrarPuestos(el.form);
        if (el.hasAttribute("data-persona-usuario")) {
            const opcion = el.selectedOptions[0];
            const nombre = el.form.querySelector("[data-persona-nombre]");
            const correo = el.form.querySelector("[data-persona-correo]");
            if (opcion && opcion.value) {
                if (nombre && !nombre.value) nombre.value = opcion.dataset.nombre || "";
                if (correo && !correo.value) correo.value = opcion.dataset.email || "";
            }
        }
    });

    document.addEventListener("gas:modal-cargado", e => {
        e.detail.querySelectorAll("form").forEach(filtrarPuestos);
    });

    /* ---------- Trabajo: proyecto interno no lleva cliente ---------- */
    function ajustarTipoTrabajo(form) {
        const tipo = form.querySelector("[data-tipo-trabajo]");
        if (!tipo) return;
        form.querySelectorAll("[data-solo-externo]").forEach(el => el.classList.toggle("d-none", tipo.value === "ProyectoInterno"));
    }
    document.addEventListener("change", e => {
        if (e.target instanceof HTMLSelectElement && e.target.hasAttribute("data-tipo-trabajo")) ajustarTipoTrabajo(e.target.form);
    });
    document.addEventListener("gas:modal-cargado", e => e.detail.querySelectorAll("form").forEach(ajustarTipoTrabajo));

    /* ---------- Tarea: entregables del trabajo elegido ---------- */
    function filtrarEntregables(form) {
        const trabajo = form.querySelector("[data-trabajo-tarea]");
        const entregable = form.querySelector("[data-entregable]");
        if (!trabajo || !entregable) return;
        [...entregable.options].forEach(o => {
            if (!o.value) return;
            const visible = trabajo.value && o.dataset.trabajo === trabajo.value;
            o.hidden = !visible;
            o.disabled = !visible;
        });
        if (entregable.selectedOptions[0]?.disabled) entregable.value = "";
    }
    document.addEventListener("change", e => {
        if (e.target instanceof HTMLSelectElement && e.target.hasAttribute("data-trabajo-tarea")) filtrarEntregables(e.target.form);
    });
    document.addEventListener("gas:modal-cargado", e => e.detail.querySelectorAll("form").forEach(filtrarEntregables));

    /* ---------- Horas: tareas del trabajo elegido ---------- */
    function filtrarTareasHoras(form) {
        const trabajo = form.querySelector("[data-horas-trabajo]");
        const tarea = form.querySelector("[data-horas-tarea]");
        if (!trabajo || !tarea) return;
        [...tarea.options].forEach(o => {
            if (!o.value) return;
            const visible = !trabajo.value ? !o.dataset.trabajo : o.dataset.trabajo === trabajo.value;
            o.hidden = !visible;
            o.disabled = !visible;
        });
        if (tarea.selectedOptions[0]?.disabled) tarea.value = "";
    }
    document.addEventListener("change", e => {
        if (e.target instanceof HTMLSelectElement && e.target.hasAttribute("data-horas-trabajo")) filtrarTareasHoras(e.target.form);
    });
    document.addEventListener("gas:modal-cargado", e => e.detail.querySelectorAll("form").forEach(filtrarTareasHoras));

    /* ---------- Movimiento: IVA, total y equivalente en pesos en vivo ---------- */
    function calcularMovimiento(form) {
        if (!form.hasAttribute("data-movimiento")) return;
        const fijo = form.hasAttribute("data-mov-fijo");
        const num = v => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };
        const moneda = fijo ? form.dataset.moneda : (form.querySelector("[data-mov-moneda]")?.value || "MXN");
        const subtotal = fijo ? num(form.dataset.subtotal) : num(form.querySelector("[data-mov-subtotal]")?.value);
        const tasa = fijo ? num(form.dataset.iva) : num(form.querySelector("[data-mov-iva]")?.value) / 100;
        const tc = moneda === "MXN" ? 1 : num(form.querySelector("[data-mov-tc-input]")?.value);
        const iva = Math.round(subtotal * tasa * 100) / 100;
        const total = subtotal + iva;
        const out = n => form.querySelector('[data-mov-out="' + n + '"]');
        if (out("iva")) out("iva").textContent = moneda + " " + GasFormato.numero(iva);
        if (out("total")) out("total").textContent = moneda + " " + GasFormato.numero(total);
        if (out("mxn")) out("mxn").textContent = tc > 0 ? "MXN " + GasFormato.numero(total * tc) : "Falta tipo de cambio";
        form.querySelectorAll("[data-mov-tc], [data-mov-mxn-row]").forEach(el => el.classList.toggle("d-none", moneda === "MXN"));
        const estado = form.querySelector("[data-mov-estado]");
        if (estado) form.querySelectorAll("[data-mov-pago]").forEach(el => el.classList.toggle("d-none", estado.value !== "Confirmado"));
    }
    ["input", "change"].forEach(tipo => document.addEventListener(tipo, e => {
        const form = e.target.closest && e.target.closest("form[data-movimiento]");
        if (form) calcularMovimiento(form);
    }));
    document.addEventListener("gas:modal-cargado", e => e.detail.querySelectorAll("form[data-movimiento]").forEach(calcularMovimiento));

    /* ---------- Filtro genérico: select[data-filtra="x"] limita las opciones de select[data-filtrado="x"] por data-padre ---------- */
    function filtrarDependientes(form) {
        form.querySelectorAll("select[data-filtra]").forEach(padre => {
            const hijo = form.querySelector('select[data-filtrado="' + padre.dataset.filtra + '"]');
            if (!hijo) return;
            [...hijo.options].forEach(o => {
                if (!o.value) return;
                const visible = !padre.value || o.dataset.padre === padre.value;
                o.hidden = !visible;
                o.disabled = !visible;
            });
            if (hijo.selectedOptions[0]?.disabled) hijo.value = "";
        });
    }
    document.addEventListener("change", e => {
        if (e.target instanceof HTMLSelectElement && e.target.hasAttribute("data-filtra") && e.target.form) filtrarDependientes(e.target.form);
    });
    document.addEventListener("gas:modal-cargado", e => e.detail.querySelectorAll("form").forEach(filtrarDependientes));

    /* ---------- Llaves: mostrar lo escrito, generar y copiar un secreto ---------- */
    document.addEventListener("click", e => {
        const boton = e.target instanceof Element ? e.target.closest("[data-mostrar-llave], [data-generar-llave], [data-copiar-llave]") : null;
        if (!boton) return;
        e.preventDefault();
        const caja = boton.closest(".col-12") || boton.parentElement;
        const input = caja.querySelector("[data-llave-secreta]");
        if (!input) return;
        if (boton.hasAttribute("data-mostrar-llave")) {
            input.type = input.type === "password" ? "text" : "password";
        } else if (boton.hasAttribute("data-generar-llave")) {
            const bytes = new Uint8Array(32);
            crypto.getRandomValues(bytes);
            input.value = btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
            input.type = "text";
            caja.querySelector("[data-aviso-generada]")?.classList.remove("d-none");
        } else if (navigator.clipboard && input.value) {
            navigator.clipboard.writeText(input.value).then(() => window.GasToast && GasToast.success("Secreto copiado."));
        }
    });

    /* ---------- Insertar un artículo de conocimiento en la respuesta ---------- */
    document.addEventListener("change", e => {
        const sel = e.target;
        if (!(sel instanceof HTMLSelectElement) || !sel.hasAttribute("data-insertar-articulo") || !sel.value) return;
        const form = sel.form;
        const opcion = sel.selectedOptions[0];
        const area = form && form.querySelector("textarea[name=texto]");
        if (area && opcion) {
            const contenido = opcion.dataset.contenido || "";
            area.value = area.value.trim() ? area.value.trimEnd() + "\n\n" + contenido : contenido;
            area.focus();
            const oculto = form.querySelector("input[name=articuloId]");
            if (oculto) oculto.value = sel.value;
        }
        sel.value = "";
    });

    /* ---------- Subida de archivos: validar tamaño antes de enviar ---------- */
    document.addEventListener("submit", e => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-subida")) return;
        const input = form.querySelector('input[type="file"]');
        const max = parseFloat(input?.dataset.maxMb || "25") * 1024 * 1024;
        if (input?.files[0] && input.files[0].size > max) {
            e.preventDefault();
            e.stopImmediatePropagation();
            window.GasToast && GasToast.error(`El archivo pesa más de ${input.dataset.maxMb || "25"} MB.`);
        }
    }, true);

    /* ---------- Modales de creación y edición ---------- */
    const modalEl = document.getElementById("gas-modal");
    if (modalEl && window.bootstrap) {
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        const contenido = document.getElementById("gas-modal-content");
        const dialogo = modalEl.querySelector(".modal-dialog");
        const CARGANDO =
            '<div class="gas-modal-loading"><span class="gas-spinner"></span> Cargando…</div>';

        const ajustarTamano = () => {
            const raiz = contenido.querySelector("[data-size]");
            const tamano = (raiz && raiz.dataset.size) || "lg";
            dialogo.classList.remove("modal-sm", "modal-lg", "modal-xl");
            if (tamano !== "md") dialogo.classList.add("modal-" + tamano);
        };

        const enfocar = () => {
            const campo = contenido.querySelector("input:not([type=hidden]):not([disabled]), select, textarea");
            if (campo) campo.focus();
        };

        async function abrirModal(url) {
            contenido.innerHTML = CARGANDO;
            dialogo.classList.remove("modal-sm", "modal-xl");
            dialogo.classList.add("modal-lg");
            modal.show();
            try {
                const r = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
                // Sesión vencida o sin permiso: ir a la página que corresponde.
                if (r.redirected && /\/Account\//.test(new URL(r.url).pathname)) {
                    window.location.href = r.url;
                    return;
                }
                if (!r.ok) throw new Error(r.status);
                contenido.innerHTML = await r.text();
                ajustarTamano();
                modalCargado();
                enfocar();
            } catch {
                modal.hide();
                window.GasToast && GasToast.error("No se pudo abrir la ventana. Inténtalo de nuevo.");
            }
        }

        window.GasModal = { abrir: abrirModal, cerrar: () => modal.hide() };

        document.addEventListener("click", e => {
            const disparador = e.target.closest("[data-modal-url]");
            if (!disparador) return;
            e.preventDefault();
            abrirModal(disparador.getAttribute("data-modal-url"));
        });

        modalEl.addEventListener("shown.bs.modal", enfocar);
        modalEl.addEventListener("hidden.bs.modal", () => { contenido.innerHTML = ""; });

        if (modalEl.dataset.abrirUrl) {
            abrirModal(modalEl.dataset.abrirUrl);
        } else if (modalEl.dataset.abrir) {
            ajustarTamano();
            modalCargado();
            modal.show();
        }
    }
})();
