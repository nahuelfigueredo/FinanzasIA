window.finanzasPwa = (() => {
    let installPrompt;
    let dotNetReference;

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        installPrompt = event;
        console.info('[FinanzasIA PWA] beforeinstallprompt recibido: la app es instalable.');
        dotNetReference?.invokeMethodAsync('InstallAvailable');
    });

    window.addEventListener('appinstalled', () => {
        console.info('[FinanzasIA PWA] appinstalled: la app se instaló correctamente.');
        installPrompt = undefined;
    });

    // Diagnóstico de instalabilidad en consola.
    setTimeout(async () => {
        if (installPrompt) {
            return;
        }
        const standalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
        console.warn('[FinanzasIA PWA] beforeinstallprompt NO se disparó después de 5s. Posibles causas:');
        console.warn('- La app ya está instalada en este dispositivo (standalone=' + standalone + ').');
        console.warn('- El prompt fue cancelado recientemente y el navegador lo bloquea temporalmente.');
        console.warn('- El navegador no soporta instalación automática (Safari iOS, Firefox).');
        if (navigator.serviceWorker) {
            const reg = await navigator.serviceWorker.getRegistration();
            console.warn('- Service worker: ' + (reg?.active ? 'activo' : 'NO activo'));
        }
    }, 5000);

    return {
        registerInstallPrompt(reference) {
            dotNetReference = reference;

            if (installPrompt) {
                dotNetReference.invokeMethodAsync('InstallAvailable');
            }
        },
        unregisterInstallPrompt() {
            dotNetReference = undefined;
        },
        canInstall() {
            return !!installPrompt;
        },
        isInstalled() {
            return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
        },
        async install() {
            if (!installPrompt) {
                return false;
            }

            installPrompt.prompt();
            const choice = await installPrompt.userChoice;
            installPrompt = undefined;
            return choice.outcome === 'accepted';
        }
    };
})();
