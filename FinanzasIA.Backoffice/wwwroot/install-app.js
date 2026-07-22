window.finanzasPwa = (() => {
	const STORAGE_KEY = 'finanzas-pwa-installed';
	let installPrompt;
	let dotNetReference;

	function isStandalone() {
		return window.matchMedia('(display-mode: standalone)').matches
			|| window.navigator.standalone === true;
	}

	function markInstalled() {
		try { localStorage.setItem(STORAGE_KEY, 'true'); } catch { /* ignore */ }
	}

	function wasInstalled() {
		try { return localStorage.getItem(STORAGE_KEY) === 'true'; } catch { return false; }
	}

	// Si corre en modo standalone, recordarlo para el navegador también.
	if (isStandalone()) {
		markInstalled();
	}

	window.addEventListener('beforeinstallprompt', event => {
		event.preventDefault();
		installPrompt = event;
		console.info('[FinanzasIA PWA] beforeinstallprompt recibido: la app es instalable.');
		// Si el navegador vuelve a ofrecer instalación, la app ya no está instalada.
		try { localStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
		dotNetReference?.invokeMethodAsync('InstallAvailable');
	});

	window.addEventListener('appinstalled', () => {
		console.info('[FinanzasIA PWA] appinstalled: la app se instaló correctamente.');
		installPrompt = undefined;
		markInstalled();
		dotNetReference?.invokeMethodAsync('InstallCompleted');
	});

	return {
		registerInstallPrompt(reference) {
			dotNetReference = reference;

			if (installPrompt && !wasInstalled() && !isStandalone()) {
				dotNetReference.invokeMethodAsync('InstallAvailable');
			}
		},
		unregisterInstallPrompt() {
			dotNetReference = undefined;
		},
		canInstall() {
			return !!installPrompt && !wasInstalled() && !isStandalone();
		},
		isInstalled() {
			return isStandalone() || wasInstalled();
		},
		async install() {
			if (!installPrompt) {
				return false;
			}

			installPrompt.prompt();
			const choice = await installPrompt.userChoice;
			installPrompt = undefined;

			if (choice.outcome === 'accepted') {
				markInstalled();
				return true;
			}
			return false;
		}
	};
})();
