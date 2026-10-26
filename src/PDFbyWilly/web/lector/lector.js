"use strict";
/*
 * Lo que PDFbyWilly / PDFSIMPLE añade al visor de pdf.js. Se carga antes que viewer.mjs (lo inyecta
 * Ventana.cs al servir viewer.html), así que puede fijar las opciones en «webviewerloaded».
 * Todo lo que necesita disco o certificados se pide al programa con fetch("/api/…").
 */
(function () {
  let app = null;
  // El nombre del programa (PDFbyWilly o PDFSIMPLE): lo pone Ventana.cs en una <meta> al servir viewer.html.
  const APP = document.querySelector("meta[name=lector-app]")?.content || "PDFbyWilly";

  async function api(metodo, cuerpo, args) {
    const cabeceras = args ? { "X-Args": encodeURIComponent(JSON.stringify(args)) } : {};
    const r = await fetch("/api/" + metodo, { method: "POST", body: cuerpo ?? null, headers: cabeceras });
    const j = await r.json();
    if (j.error) throw new Error(j.error);
    return j;
  }

  // ------------------------------------------------------------ avisos

  let temporizador = null;
  function aviso(texto, error, ms) {
    let el = document.getElementById("lectorAviso");
    if (!el) {
      el = document.createElement("div");
      el.id = "lectorAviso";
      el.addEventListener("click", () => el.classList.remove("visible"));
      document.body.append(el);
    }
    el.textContent = texto;
    el.classList.toggle("error", !!error);
    el.classList.add("visible");
    clearTimeout(temporizador);
    temporizador = setTimeout(() => el.classList.remove("visible"), ms ?? (error ? 9000 : 4000));
  }

  // ------------------------------------------------------------ verificación de firmas

  const verificador = {
    async verify(sig) {
      const [a, b] = sig.data;
      const cuerpo = new Uint8Array(a.length + b.length + sig.pkcs7.length);
      cuerpo.set(a, 0);
      cuerpo.set(b, a.length);
      cuerpo.set(sig.pkcs7, a.length + b.length);
      const r = await api("verificar", cuerpo, {
        largo: a.length + b.length,
        subFilter: sig.subFilter,
        signingTime: sig.signingTime,
      });
      return {
        ...r,
        documentModifiedAfterSigning: !sig.coversWholeDocument,
        modificationsAfterSignature: sig.modificationsAfterSignature,
      };
    },
    viewCertificate(cert) {
      api("vercert", JSON.stringify({ der: cert.der })).catch(e => aviso(e.message, true));
    },
  };

  // ------------------------------------------------------------ opciones de pdf.js

  document.addEventListener("webviewerloaded", () => {
    const o = window.PDFViewerApplicationOptions;
    o.set("disablePreferences", true);
    o.set("defaultUrl", "");
    o.set("localeProperties", { lang: "es-ES" });
    o.set("enableSignatureEditor", true);      // «Añadir firma»: dibujada, escrita o imagen
    o.set("enableSignatureVerification", true); // panel de firmas digitales
    o.set("externalLinkTarget", 2);            // enlaces del PDF, al navegador
    o.set("disableRange", true);
    o.set("disableStream", true);
    o.set("historyUpdateUrl", false);

    app = window.PDFViewerApplication;
    app.externalServices.createSignatureVerifier = () => verificador;
    app.initializedPromise.then(iniciar);
  });

  function iniciar() {
    // Página de abrir/guardar del propio pdf.js: aquí van al programa.
    interceptar("downloadButton", () => guardar(false));
    interceptar("secondaryDownload", () => guardar(false));
    interceptar("secondaryOpenFile", abrir);

    añadirBotones();
    recargar();
  }

  function interceptar(id, fn) {
    const el = document.getElementById(id);
    el?.addEventListener("click", e => {
      e.preventDefault();
      e.stopImmediatePropagation();
      document.getElementById("secondaryToolbar")?.classList.add("hidden");
      fn();
    }, true);
  }

  function boton(id, titulo, etiqueta, fn) {
    const b = document.createElement("button");
    b.id = id;
    b.className = "toolbarButton";
    b.type = "button";
    b.title = titulo;
    const s = document.createElement("span");
    s.textContent = etiqueta;
    b.append(s);
    b.addEventListener("click", fn);
    return b;
  }

  function añadirBotones() {
    const descargar = document.getElementById("downloadButton");
    descargar.title = "Guardar (Ctrl+S)";
    const abrirBtn = boton("lectorAbrir", "Abrir (Ctrl+O)", "Abrir", abrir);
    const firmarBtn = boton("lectorFirmar", "Firmar con certificado digital", "Firmar", firmarConCertificado);
    // Abrir, lo primero de la barra. Firmar, justo después del grupo de Imprimir/Guardar: ese grupo
    // (hiddenMediumView) lo esconde pdf.js cuando la ventana es estrecha, y estos dos no deben desaparecer.
    document.getElementById("toolbarViewerLeft").prepend(abrirBtn);
    descargar.parentElement.after(firmarBtn);

    // «Guardar como…» en el menú de herramientas, debajo de Abrir.
    const menuAbrir = document.getElementById("secondaryOpenFile");
    const como = document.createElement("button");
    como.id = "lectorGuardarComo";
    como.className = "toolbarButton labeled";
    como.type = "button";
    const s = document.createElement("span");
    s.textContent = "Guardar como…";
    como.append(s);
    como.addEventListener("click", () => {
      document.getElementById("secondaryToolbar")?.classList.add("hidden");
      guardar(true);
    });
    menuAbrir?.after(como);

    // Al final del menú: dejarlo como programa para abrir los PDF.
    const pred = document.createElement("button");
    pred.id = "lectorPredeterminado";
    pred.className = "toolbarButton labeled";
    pred.type = "button";
    const sp = document.createElement("span");
    sp.textContent = "Abrir siempre los PDF con " + APP + "…";
    pred.append(sp);
    pred.addEventListener("click", () => {
      document.getElementById("secondaryToolbar")?.classList.add("hidden");
      api("predeterminado")
        .then(r => r.ok && aviso("En Configuración: escribe .pdf en el buscador de arriba, pulsa la casilla que sale debajo, elige " + APP + " y «Establecer predeterminado».", false, 30000))
        .catch(e => aviso(e.message, true));
    });
    const menu = document.getElementById("secondaryToolbarButtonContainer");
    const sep = document.createElement("div");
    sep.className = "horizontalToolbarSeparator";
    menu?.append(sep, pred);
  }

  // ------------------------------------------------------------ abrir / guardar

  async function recargar() {
    const est = await api("estado");
    if (!est.hay) return;
    await app.open({ url: "/doc/" + est.version + "/" + encodeURIComponent(est.nombre) });
  }

  async function abrir() {
    try {
      await api("abrir");
    } catch (e) {
      aviso("No se ha podido abrir: " + e.message, true);
    }
  }

  /** Los bytes del documento con lo rellenado, escrito y dibujado (pdf.js lo añade al final). */
  async function bytesActuales() {
    app.pdfViewer?._layerProperties?.annotationEditorUIManager?.endCurrentEditing?.();
    const doc = app.pdfDocument;
    if (doc.annotationStorage.size > 0) {
      await app.pdfScriptingManager?.dispatchWillSave?.();
      try {
        return await doc.saveDocument();
      } finally {
        await app.pdfScriptingManager?.dispatchDidSave?.();
      }
    }
    return await doc.getData();
  }

  let ocupado = false;
  async function guardar(como, cerrar) {
    if (!app?.pdfDocument || ocupado) return false;
    ocupado = true;
    document.body.classList.add("lector-esperando");
    try {
      const bytes = await bytesActuales();
      const r = await api("guardar", bytes, { como: !!como, cerrar: !!cerrar, nombre: app._docFilename });
      if (r.cancelado) return false;
      if (!cerrar) {
        await recargar();
        aviso("Guardado: " + r.nombre);
      }
      return true;
    } catch (e) {
      aviso("No se ha podido guardar: " + e.message, true);
      return false;
    } finally {
      ocupado = false;
      document.body.classList.remove("lector-esperando");
    }
  }

  // ------------------------------------------------------------ firmar con certificado

  let colocando = null;

  function firmarConCertificado() {
    if (!app?.pdfDocument || ocupado || colocando) return;
    app.eventBus.dispatch("switchannotationeditormode", { source: null, mode: 0 });

    const barra = document.createElement("div");
    barra.id = "lectorBarraFirma";
    const txt = document.createElement("span");
    txt.textContent = "Arrastra en la página el recuadro donde irá la firma (o haz clic para uno estándar).";
    const invisible = document.createElement("button");
    invisible.textContent = "Firma invisible";
    invisible.addEventListener("click", () => terminarColocar(null));
    const cancelar = document.createElement("button");
    cancelar.textContent = "Cancelar";
    cancelar.addEventListener("click", cancelarColocar);
    barra.append(txt, invisible, cancelar);
    document.body.append(barra);
    document.body.classList.add("lector-colocando");

    const cont = document.getElementById("viewerContainer");
    colocando = { barra, cont, abajo: null, marco: null };
    cont.addEventListener("pointerdown", alBajar, true);
  }

  function paginaDe(target) {
    const div = target.closest?.(".page");
    if (!div) return null;
    const n = +div.dataset.pageNumber;
    return { n, vista: app.pdfViewer.getPageView(n - 1), div };
  }

  function puntoEnPagina(pag, e) {
    const r = pag.div.getBoundingClientRect();
    return [e.clientX - r.left - pag.div.clientLeft, e.clientY - r.top - pag.div.clientTop];
  }

  function alBajar(e) {
    const pag = paginaDe(e.target);
    if (!pag || e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation();
    const ini = puntoEnPagina(pag, e);
    const marco = document.createElement("div");
    marco.className = "lectorMarcoFirma";
    pag.div.append(marco);
    colocando.abajo = { pag, ini };
    colocando.marco = marco;
    pintar(ini, ini);
    window.addEventListener("pointermove", alMover, true);
    window.addEventListener("pointerup", alSoltar, true);
  }

  function pintar(a, b) {
    const m = colocando.marco;
    m.style.left = Math.min(a[0], b[0]) + "px";
    m.style.top = Math.min(a[1], b[1]) + "px";
    m.style.width = Math.abs(b[0] - a[0]) + "px";
    m.style.height = Math.abs(b[1] - a[1]) + "px";
  }

  function alMover(e) {
    if (!colocando?.abajo) return;
    e.preventDefault();
    pintar(colocando.abajo.ini, puntoEnPagina(colocando.abajo.pag, e));
  }

  function alSoltar(e) {
    window.removeEventListener("pointermove", alMover, true);
    window.removeEventListener("pointerup", alSoltar, true);
    if (!colocando?.abajo) return;
    e.preventDefault();
    const { pag, ini } = colocando.abajo;
    let fin = puntoEnPagina(pag, e);
    const vp = pag.vista.viewport;
    if (Math.abs(fin[0] - ini[0]) < 12 || Math.abs(fin[1] - ini[1]) < 8) {
      // Un clic: recuadro estándar de 190 × 55 puntos desde ahí.
      fin = [ini[0] + 190 * vp.scale, ini[1] + 55 * vp.scale];
      pintar(ini, fin);
    }
    const [x1, y1] = vp.convertToPdfPoint(ini[0], ini[1]);
    const [x2, y2] = vp.convertToPdfPoint(fin[0], fin[1]);
    terminarColocar({ pagina: pag.n, rect: [Math.min(x1, x2), Math.min(y1, y2), Math.max(x1, x2), Math.max(y1, y2)] });
  }

  function limpiarColocar() {
    if (!colocando) return;
    colocando.cont.removeEventListener("pointerdown", alBajar, true);
    colocando.barra.remove();
    colocando.marco?.remove();
    document.body.classList.remove("lector-colocando");
    colocando = null;
  }

  function cancelarColocar() {
    limpiarColocar();
  }

  async function terminarColocar(sitio) {
    const marco = colocando?.marco;
    colocando.cont.removeEventListener("pointerdown", alBajar, true);
    colocando.barra.remove();
    document.body.classList.remove("lector-colocando");
    colocando = null;

    ocupado = true;
    document.body.classList.add("lector-esperando");
    try {
      const bytes = await bytesActuales();
      const r = await api("firmar", bytes, sitio ?? { pagina: 1, rect: null });
      if (r.cancelado) return;
      await recargar();
      aviso(r.aviso ? "Firmado y guardado como " + r.nombre + ". " + r.aviso : "Firmado y guardado como " + r.nombre, !!r.aviso);
    } catch (e) {
      aviso("No se ha podido firmar: " + e.message, true);
    } finally {
      marco?.remove();
      ocupado = false;
      document.body.classList.remove("lector-esperando");
    }
  }

  // ------------------------------------------------------------ teclado y arrastrar

  window.addEventListener("keydown", e => {
    if (colocando && e.key === "Escape") {
      e.preventDefault();
      e.stopImmediatePropagation();
      cancelarColocar();
      return;
    }
    if (!e.ctrlKey || e.altKey || e.metaKey) return;
    const k = e.key.toLowerCase();
    let fn = null;
    if (k === "s") fn = () => guardar(e.shiftKey);
    else if (k === "o" && !e.shiftKey) fn = abrir;
    if (fn) {
      e.preventDefault();
      e.stopImmediatePropagation();
      fn();
    }
  }, true);

  const conPdf = e => [...(e.dataTransfer?.items ?? [])].some(i => i.kind === "file" && (i.type === "application/pdf" || i.type === ""));
  window.addEventListener("dragover", e => {
    if (conPdf(e)) {
      e.preventDefault();
      e.stopImmediatePropagation();
      e.dataTransfer.dropEffect = "copy";
    }
  }, true);
  window.addEventListener("drop", e => {
    const archivos = [...(e.dataTransfer?.files ?? [])].filter(f => /\.pdf$/i.test(f.name));
    if (!archivos.length) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    window.chrome?.webview?.postMessageWithAdditionalObjects("soltar", archivos);
  }, true);

  // Al entrar en una casilla de texto se selecciona todo; un segundo clic deja el cursor normal.
  let _selArm = null;
  document.addEventListener("focusin", e => {
    const t = e.target;
    if (t && t.tagName === "INPUT" && /^(text|number|tel|email|password|search|url)$/i.test(t.type || "text")) {
      try { t.select(); _selArm = t; } catch (x) {}
    }
  });
  document.addEventListener("mouseup", e => {
    if (_selArm && e.target === _selArm) e.preventDefault();
    _selArm = null;
  });

  // Para el programa: ¿hay algo sin guardar? y guardar antes de cerrar.
  window.lector = {
    hayCambios: () => !!app?.pdfDocument && !!app._annotationStorageModified,
    guardar,
    recargar,
    aviso: (texto, ms) => aviso(texto, false, ms),
  };
})();
