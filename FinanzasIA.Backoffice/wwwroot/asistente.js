// Historial de conversaciones del Asistente Financiero (localStorage).
window.finanzasAsistente = (() => {
    const KEY = 'finanzas-asistente-conversaciones';
    const MAX = 50;

    function load() {
        try {
            return JSON.parse(localStorage.getItem(KEY)) ?? [];
        } catch {
            return [];
        }
    }

    function save(list) {
        try {
            localStorage.setItem(KEY, JSON.stringify(list.slice(0, MAX)));
        } catch { /* storage lleno o no disponible */ }
    }

    return {
        listar() {
            return load().map(c => ({
                id: c.id,
                titulo: c.titulo,
                fecha: c.fecha,
                cantidadMensajes: (c.mensajes ?? []).length
            }));
        },
        obtener(id) {
            return load().find(c => c.id === id) ?? null;
        },
        guardar(conversacion) {
            const list = load();
            const index = list.findIndex(c => c.id === conversacion.id);
            if (index >= 0) {
                list[index] = conversacion;
            } else {
                list.unshift(conversacion);
            }
            // La conversación más reciente primero.
            list.sort((a, b) => (b.fecha ?? '').localeCompare(a.fecha ?? ''));
            save(list);
        },
        renombrar(id, titulo) {
            const list = load();
            const conv = list.find(c => c.id === id);
            if (conv) {
                conv.titulo = titulo;
                save(list);
            }
        },
        eliminar(id) {
            save(load().filter(c => c.id !== id));
        }
    };
})();
