window.finanzasTheme = (() => {
	const storageKey = 'finanzas-theme';

	function prefersDark() {
		return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
	}

	function getStoredTheme() {
		return localStorage.getItem(storageKey);
	}

	function applyTheme(theme) {
		theme = theme === 'dark' ? 'dark' : 'light';
		document.documentElement.dataset.theme = theme;
		localStorage.setItem(storageKey, theme);
		return theme === 'dark';
	}

	function initialize() {
		const theme = getStoredTheme() || (prefersDark() ? 'dark' : 'light');
		return applyTheme(theme);
	}

	initialize();

	return {
		initialize,
		get() {
			return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';
		},
		set(theme) {
			return applyTheme(theme);
		},
		toggle() {
			const current = document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';
			return applyTheme(current === 'dark' ? 'light' : 'dark');
		}
	};
})();
