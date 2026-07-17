window.finanzasPwa = (() => {
    let installPrompt;
    let dotNetReference;

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        installPrompt = event;
        dotNetReference?.invokeMethodAsync('InstallAvailable');
    });

    window.addEventListener('appinstalled', () => {
        installPrompt = undefined;
    });

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
