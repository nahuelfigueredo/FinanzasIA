window.finanzasPreferences = (() => {
    const storageKey = 'finanzas-preferences';
    const defaults = {
        userName: 'Usuario',
        currency: 'ARS',
        monthlyBudget: 100000,
        fiscalDay: 1,
        showDemoHint: true
    };

    function normalize(preferences) {
        const merged = { ...defaults, ...(preferences || {}) };
        merged.monthlyBudget = Number(merged.monthlyBudget) || defaults.monthlyBudget;
        merged.fiscalDay = Number(merged.fiscalDay) || defaults.fiscalDay;
        return merged;
    }

    function get() {
        const raw = localStorage.getItem(storageKey);
        if (!raw) {
            return normalize();
        }

        try {
            return normalize(JSON.parse(raw));
        } catch {
            return normalize();
        }
    }

    function set(preferences) {
        const normalized = normalize(preferences);
        localStorage.setItem(storageKey, JSON.stringify(normalized));
        return normalized;
    }

    return { get, set };
})();

window.finanzasUi = {
    confirm(message) {
        return window.confirm(message);
    },
    toast(message, type) {
        const containerId = 'finanzas-toast-container';
        let container = document.getElementById(containerId);

        if (!container) {
            container = document.createElement('div');
            container.id = containerId;
            container.className = 'toast-stack';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `app-toast ${type || 'info'}`;
        toast.textContent = message;
        container.appendChild(toast);

        window.setTimeout(() => toast.classList.add('show'), 10);
        window.setTimeout(() => {
            toast.classList.remove('show');
            window.setTimeout(() => toast.remove(), 250);
        }, 3200);
    }
};
