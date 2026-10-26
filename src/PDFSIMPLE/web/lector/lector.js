"use strict";
/*
 * Lo que PDFSIMPLE añade al visor de pdf.js. Se carga antes que viewer.mjs (lo inyecta
 * Ventana.cs al servir viewer.html), así que puede fijar las opciones en «webviewerloaded».
 * Todo lo que necesita disco o certificados se pide al programa con fetch("/api/…").
 */
(function () {
  let app = null;
  // El nombre del programa: lo pone Ventana.cs en una <meta> al servir viewer.html.
  const APP = document.querySelector("meta[name=lector-app]")?.content || "PDFSIMPLE";
  // El idioma (es / fr / en) y la carpeta de traducciones de pdf.js: también los pone Ventana.cs.
  const IDIOMA = document.querySelector("meta[name=lector-idioma]")?.content || "es";
  const IDIOMA_PDFJS = document.querySelector("meta[name=lector-idioma-pdfjs]")?.content || "es-ES";
  const IDIOMAS = [["es", "Español"], ["fr", "Français"], ["en", "English"]];
  // «--vista=…» del programa: zoom=…, panel=1|0 (miniaturas abiertas o cerradas), barra=completa (sin recoger). Para capturas.
  const VISTA = new URLSearchParams(document.querySelector("meta[name=lector-vista]")?.content || "");

  // Los textos de lo nuestro: [español, francés, inglés]. Los de pdf.js vienen en sus propias traducciones.
  const TEXTOS = {
    abrir: ["Abrir (Ctrl+O)", "Ouvrir (Ctrl+O)", "Open (Ctrl+O)"],
    abrirCorto: ["Abrir", "Ouvrir", "Open"],
    guardar: ["Guardar (Ctrl+S)", "Enregistrer (Ctrl+S)", "Save (Ctrl+S)"],
    guardarComo: ["Guardar como…", "Enregistrer sous…", "Save as…"],
    firmar: ["Firmar", "Signer", "Sign"],
    firmarTitulo: ["Firmar con certificado digital", "Signer avec un certificat numérique", "Sign with a digital certificate"],
    predeterminado: ["Abrir siempre los PDF con {0}…", "Toujours ouvrir les PDF avec {0}…", "Always open PDFs with {0}…"],
    comoPredeterminado: [
      "En Configuración: escribe .pdf en el buscador de arriba, pulsa la casilla que sale debajo, elige {0} y «Establecer predeterminado».",
      "Dans Paramètres : tapez .pdf dans la recherche en haut, cliquez sur la case qui apparaît dessous, choisissez {0} puis « Définir par défaut ».",
      "In Settings: type .pdf in the search box at the top, click the box that appears below, choose {0} and “Set default”."],
    mostrarIconos: ["Mostrar todos los iconos", "Afficher toutes les icônes", "Show all icons"],
    recogerIconos: ["Recoger iconos en el menú »", "Ranger les icônes dans le menu »", "Tuck icons into the » menu"],
    panel: ["Mostrar u ocultar el panel lateral (F4)", "Afficher ou masquer le panneau latéral (F4)", "Show or hide the sidebar (F4)"],
    idioma: ["Idioma", "Langue", "Language"],
    perderCambios: [
      "Hay cambios sin guardar y se perderán al cambiar el idioma. ¿Cambiar igualmente?",
      "Des modifications ne sont pas enregistrées et seront perdues en changeant de langue. Continuer ?",
      "There are unsaved changes that will be lost when changing the language. Continue?"],
    noAbrir: ["No se ha podido abrir: ", "Impossible d'ouvrir : ", "Could not open: "],
    guardado: ["Guardado: ", "Enregistré : ", "Saved: "],
    noGuardar: ["No se ha podido guardar: ", "Impossible d'enregistrer : ", "Could not save: "],
    barraFirma: [
      "Arrastra en la página el recuadro donde irá la firma (o haz clic para uno estándar).",
      "Tracez sur la page le cadre de la signature (ou cliquez pour un cadre standard).",
      "Drag a box on the page where the signature will go (or click for a standard one)."],
    firmaInvisible: ["Firma invisible", "Signature invisible", "Invisible signature"],
    cancelar: ["Cancelar", "Annuler", "Cancel"],
    firmadoComo: ["Firmado y guardado como ", "Signé et enregistré sous ", "Signed and saved as "],
    noFirmar: ["No se ha podido firmar: ", "Impossible de signer : ", "Could not sign: "],
    error: ["Error: ", "Erreur : ", "Error: "],
  };
  const t = (clave, ...args) => {
    const s = (TEXTOS[clave] ?? [clave])[{ es: 0, fr: 1, en: 2 }[IDIOMA] ?? 0] ?? TEXTOS[clave][0];
    return s.replace(/\{(\d)\}/g, (_, i) => args[i]);
  };

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
    o.set("localeProperties", { lang: IDIOMA_PDFJS });
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

  // Un fallo de lo nuestro no debe quedar mudo: se enseña abajo.
  window.addEventListener("error", e => /ResizeObserver loop/.test(e.message) || aviso(t("error") + e.message + " (" + (e.filename || "").split("/").pop() + ":" + e.lineno + ")", true));
  window.addEventListener("unhandledrejection", e => aviso(t("error") + (e.reason?.message || e.reason), true));

  function iniciar() {
    // Página de abrir/guardar del propio pdf.js: aquí van al programa.
    interceptar("downloadButton", () => guardar(false));
    interceptar("secondaryDownload", () => guardar(false));
    interceptar("secondaryOpenFile", abrir);

    añadirBotones();
    camposDelScript();
    recargar();
  }

  /**
   * Los campos de solo lectura que rellena el JavaScript del PDF, como el código de barras de los modelos 790 de
   * Hacienda: pdf.js los tapa con una casilla HTML en letra normal (se ve «ööE1Aã1Fã~» y no las barras). Aquí se
   * pintan en la página con la fuente del propio PDF: el dibujo de pantalla recibe los valores del formulario
   * (pdf.js solo se los da al imprimir) y la página se vuelve a pintar cuando el script los cambia.
   */
  function camposDelScript() {
    app.eventBus.on("documentloaded", () => {
      const transporte = app.pdfDocument?._transport;
      if (!transporte?.getRenderingIntent || transporte.lectorParcheado) return;
      transporte.lectorParcheado = true;
      // Qué claves del almacén son valores de campos: las anotaciones nuevas (texto, firmas…) tienen su propia capa.
      const almacen = app.pdfDocument.annotationStorage, campos = new Set();
      const poner = almacen.setValue.bind(almacen);
      almacen.setValue = (clave, valor) => {
        if (valor && typeof valor === "object" && typeof valor.serialize !== "function" && "value" in valor) campos.add(clave);
        return poner(clave, valor);
      };
      const original = transporte.getRenderingIntent.bind(transporte);
      transporte.getRenderingIntent = (intent, modo, ...resto) => {
        const r = original(intent, modo, ...resto);
        if (intent !== "display" || modo !== pdfjsLib.AnnotationMode.ENABLE_FORMS || r.annotationStorageSerializable.map || !campos.size) return r;
        try {
          const map = new Map();
          for (const clave of campos) {
            const v = almacen.getRawValue(clave);
            if (v && typeof v === "object") map.set(clave, { ...v });
          }
          const hash = JSON.stringify([...map]);
          return { ...r, cacheKey: r.cacheKey + "_" + hash, annotationStorageSerializable: { map, hash, transfer: undefined } };
        } catch {
          return r;
        }
      };
    });
    let espera = 0;
    document.addEventListener("updatefromsandbox", e => {
      if (!e.detail || !("value" in e.detail) || !e.target.closest?.(".textWidgetAnnotation.hasOwnCanvas")) return;
      clearTimeout(espera);
      espera = setTimeout(() => app.pdfViewer.refresh(), 150);
    }, true);
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

  // ------------------------------------------------------------ la barra de iconos
  //
  // [Abrir][Guardar][Guardar como] | imprimir | imagen, texto, dibujar, firma manuscrita, selección, mano |
  // girar ← → | Firmar con certificado + ✓ firmas | desplazamiento | vista de libro | lo demás …   [文A][‹›][»]
  //
  // Abajo en el centro, flotando: ↑ ↓ página x de x | zoom.
  // Por defecto se ve todo. Al estrechar la ventana las secciones van pasando, desde la derecha, al menú «»»,
  // que solo aparece cuando tiene algo. La flecha ‹› recoge en él las secciones que antes eran del menú
  // (girar, desplazamiento, vista de libro, lo demás) y se recuerda.

  const $ = id => document.getElementById(id);
  const CLAVE_PLEGADA = "lector.barraRecogida"; // sin guardar: todo a la vista
  let secciones = [];
  let extras = null;
  let plegar = null;

  function separador() {
    const d = document.createElement("div");
    d.className = "verticalToolbarSeparator";
    return d;
  }

  function botonMenu(id, titulo, fn) {
    const b = boton(id, titulo, titulo, () => {
      $("secondaryToolbar")?.classList.add("hidden");
      fn();
    });
    return b;
  }

  function añadirBotones() {
    const izquierda = $("toolbarViewerLeft");

    // Abrir, Guardar y Guardar como: lo primero, siempre a la vista.
    const descargar = $("downloadButton");
    descargar.title = t("guardar");
    const archivo = document.createElement("div");
    archivo.id = "lectorArchivo";
    archivo.className = "toolbarHorizontalGroup";
    archivo.append(boton("lectorAbrir", t("abrir"), t("abrirCorto"), abrir), descargar,
      boton("lectorGuardarComo", t("guardarComo"), t("guardarComo"), () => guardar(true)));

    // El panel lateral se abre y se cierra con un triángulo en su borde, no con el botón de la barra.
    const pestaña = document.createElement("button");
    pestaña.id = "lectorPanel";
    pestaña.type = "button";
    pestaña.title = t("panel");
    pestaña.addEventListener("click", () => $("viewsManagerToggleButton").click());
    $("mainContainer").append(pestaña);

    // Abajo en el centro: ↑ ↓, página x de x y el zoom. Sigue dentro de la barra (para sus estilos) pero fijo abajo.
    const pie = document.createElement("div");
    pie.id = "lectorPie";
    pie.className = "toolbarHorizontalGroup";
    const flechas = $("previous").parentElement;
    const pagina = $("numPages").parentElement;
    for (const el of [flechas, pagina]) el.classList.remove("hiddenSmallView", "hiddenMediumView");
    pie.append(flechas, pagina, separador(), ...$("toolbarViewerMiddle").children);
    $("toolbarContainer").append(pie);

    // Las secciones, en este orden. «plegable»: la flecha ‹› las recoge en el menú.
    const firmarBtn = boton("lectorFirmar", t("firmarTitulo"), t("firmar"), firmarConCertificado);
    const predeterminado = botonMenu("lectorPredeterminado", t("predeterminado", APP), () => {
      api("predeterminado")
        .then(r => r.ok && aviso(t("comoPredeterminado", APP), false, 30000))
        .catch(e => aviso(e.message, true));
    });
    const grupos = [
      { plegable: false, els: [$("printButton")] },
      { plegable: false, els: [$("editorStamp"), $("editorFreeText"), $("editorInk"), $("editorSignature"), $("cursorSelectTool"), $("cursorHandTool")] },
      { plegable: true, els: [$("pageRotateCcw"), $("pageRotateCw")] },
      { plegable: false, els: [firmarBtn, $("signatureProperties")] },
      { plegable: true, els: [$("scrollModeButtons")] },
      { plegable: true, els: [$("spreadModeButtons")] },
      { plegable: true, els: [$("editorHighlight"), $("editorComment"), $("viewFindButton").parentElement, $("presentationMode"),
                             $("firstPage"), $("lastPage"), $("documentProperties"), predeterminado] },
      // El idioma, lo último: se usa poco y es lo primero que se va al menú.
      { plegable: true, els: [selectorIdioma(), listaIdiomas()] },
    ];

    // Lo que no se usa (duplicados del menú, separadores y huecos de pdf.js…) se aparta sin borrarlo:
    // pdf.js sigue usando esos elementos.
    const apartado = document.createElement("div");
    apartado.hidden = true;
    for (const g of grupos) for (const el of g.els) el.remove();
    apartado.append(...$("secondaryToolbarButtonContainer").children);
    for (const el of [...izquierda.children, ...$("toolbarViewerRight").children])
      if (el.id !== "viewsManager" && el.id !== "secondaryToolbarToggle") apartado.append(el);
    document.body.append(apartado);

    extras = document.createElement("div");
    extras.id = "lectorExtras";
    extras.className = "toolbarHorizontalGroup";
    secciones = grupos.map(g => {
      const s = document.createElement("div");
      s.className = "lectorSeccion toolbarHorizontalGroup";
      s.dataset.plegable = g.plegable ? "1" : "";
      s.append(...g.els.filter(Boolean));
      return s;
    });
    izquierda.prepend(archivo, extras);

    plegar = boton("lectorPlegar", "", "", () => {
      try { localStorage.setItem(CLAVE_PLEGADA, estaPlegada() ? "0" : "1"); } catch {}
      colocar();
    });
    $("secondaryToolbarToggle").before(plegar);

    colocar();
    new ResizeObserver(() => requestAnimationFrame(colocar)).observe($("toolbarContainer"));
    app.eventBus.on("pagesloaded", () => requestAnimationFrame(colocar));
    app.eventBus.on("pagesloaded", aplicarVista, { once: true });
    setTimeout(colocar, 800); // los textos traducidos llegan algo después y cambian anchos
  }
  function aplicarVista() {
    if (VISTA.get("zoom")) app.pdfViewer.currentScaleValue = VISTA.get("zoom");
    const abierto = $("outerContainer").classList.contains("viewsManagerOpen");
    if ((VISTA.get("panel") === "1" && !abierto) || (VISTA.get("panel") === "0" && abierto)) $("viewsManagerToggleButton").click();
  }

  /** El icono de idiomas (文A) con su menú: español, francés, inglés. */
  function selectorIdioma() {
    const caja = document.createElement("div");
    caja.id = "lectorIdioma";
    caja.className = "toolbarButtonWithContainer";
    const menu = document.createElement("div");
    menu.id = "lectorIdiomaMenu";
    menu.className = "hidden doorHangerRight menu";
    const lista = document.createElement("div");
    lista.className = "menuContainer";
    for (const [codigo, nombre] of IDIOMAS) {
      const b = boton("lectorIdioma-" + codigo, nombre, nombre, () => cambiarIdioma(codigo));
      b.classList.add("labeled");
      b.classList.toggle("toggled", codigo === IDIOMA);
      lista.append(b);
    }
    menu.append(lista);
    const abrirMenu = boton("lectorIdiomaBoton", t("idioma"), t("idioma"), e => {
      e.stopPropagation();
      menu.classList.toggle("hidden");
    });
    document.addEventListener("click", e => { if (!caja.contains(e.target)) menu.classList.add("hidden"); });
    caja.append(abrirMenu, menu);
    return caja;
  }

  /** Los tres idiomas como opciones sueltas: es lo que se ve cuando la sección está en el menú «»». */
  function listaIdiomas() {
    const lista = document.createElement("div");
    lista.id = "lectorIdiomasEnMenu";
    for (const [codigo, nombre] of IDIOMAS) {
      const b = botonMenu("lectorIdiomaMenu-" + codigo, nombre, () => cambiarIdioma(codigo));
      b.classList.toggle("toggled", codigo === IDIOMA);
      lista.append(b);
    }
    return lista;
  }

  async function cambiarIdioma(codigo) {
    $("lectorIdiomaMenu").classList.add("hidden");
    if (codigo === IDIOMA) return;
    if (window.lector.hayCambios()) {
      if (!confirm(t("perderCambios"))) return;
      app.pdfDocument?.annotationStorage.resetModified?.();
      app._annotationStorageModified = false;
    }
    try {
      await api("idioma", null, { idioma: codigo }); // el programa lo guarda y recarga el visor
    } catch (e) {
      aviso(t("error") + e.message, true);
    }
  }

  function estaPlegada() {
    if (VISTA.get("barra") === "completa") return false;
    try { return localStorage.getItem(CLAVE_PLEGADA) === "1"; } catch { return false; }
  }

  /** En la barra, iconos; en el menú, con su texto. */
  function modo(seccion, enBarra) {
    seccion.classList.toggle("toolbarHorizontalGroup", enBarra);
    // Solo los botones de la sección: los de dentro de sus desplegables (colores, buscar, idiomas…) no se tocan.
    for (const b of seccion.querySelectorAll(".toolbarButton"))
      if (!b.parentElement.closest(".editorParamsToolbar, .menu, #findbar")) b.classList.toggle("labeled", !enBarra);
    for (const g of seccion.querySelectorAll("[role=radiogroup]")) g.classList.toggle("toolbarHorizontalGroup", enBarra);
  }

  function desborda() {
    const barra = $("toolbarViewer");
    const fin = extras.getBoundingClientRect().right; // donde acaban de verdad los iconos de la izquierda
    const der = $("toolbarViewerRight").getBoundingClientRect();
    return barra.scrollWidth > barra.clientWidth + 1 || fin > der.left - 4 || der.right > Math.min(barra.getBoundingClientRect().right, document.documentElement.clientWidth) + 1;
  }

  let recolocando = false;
  function colocar() {
    if (!extras || recolocando) return;
    recolocando = true;
    const plegada = estaPlegada();
    const menu = $("secondaryToolbarButtonContainer");

    // Todas a la barra y, si no caben (o está plegada), van saliendo por la derecha hacia el menú.
    extras.replaceChildren();
    for (const s of secciones) {
      modo(s, true);
      extras.append(separador(), s);
    }
    const alMenu = [];
    for (let i = secciones.length - 1; i >= 0; i--) {
      const s = secciones[i];
      if (!((plegada && s.dataset.plegable) || desborda())) continue;
      s.previousElementSibling?.remove();
      s.remove();
      alMenu.unshift(s);
    }
    menu.replaceChildren();
    alMenu.forEach((s, i) => {
      modo(s, false);
      if (i > 0) {
        const h = document.createElement("div");
        h.className = "horizontalToolbarSeparator";
        menu.append(h);
      }
      menu.append(s);
    });
    $("secondaryToolbarToggle").hidden = alMenu.length === 0;
    if (alMenu.length === 0) $("secondaryToolbar").classList.add("hidden");

    // Los desplegables de cada herramienta (colores, grosor, buscar…) se abren hacia donde haya sitio.
    for (const d of extras.querySelectorAll(".doorHanger, .doorHangerRight")) {
      const aLaDerecha = d.parentElement.getBoundingClientRect().left > window.innerWidth / 2;
      d.classList.toggle("doorHangerRight", aLaDerecha);
      d.classList.toggle("doorHanger", !aLaDerecha);
    }

    // La flecha: «›» recoge las secciones hacia el borde; «‹» las saca.
    plegar.classList.toggle("plegada", plegada);
    plegar.title = plegada ? t("mostrarIconos") : t("recogerIconos");
    recolocando = false;
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
      aviso(t("noAbrir") + e.message, true);
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
        aviso(t("guardado") + r.nombre);
      }
      return true;
    } catch (e) {
      aviso(t("noGuardar") + e.message, true);
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
    txt.textContent = t("barraFirma");
    const invisible = document.createElement("button");
    invisible.textContent = t("firmaInvisible");
    invisible.addEventListener("click", () => terminarColocar(null));
    const cancelar = document.createElement("button");
    cancelar.textContent = t("cancelar");
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
      aviso(r.aviso ? t("firmadoComo") + r.nombre + ". " + r.aviso : t("firmadoComo") + r.nombre, !!r.aviso);
    } catch (e) {
      aviso(t("noFirmar") + e.message, true);
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
