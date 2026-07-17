window.finanzasFx = {
    countUp: function () {
        const elements = document.querySelectorAll("[data-countup]:not([data-countup-done])");
        elements.forEach(el => {
            el.setAttribute("data-countup-done", "true");
            const finalText = el.textContent;
            const match = finalText.match(/-?[\d.,]+/);
            if (!match) {
                return;
            }

            const numericText = match[0];
            const prefix = finalText.slice(0, match.index);
            const suffix = finalText.slice(match.index + numericText.length);

            // Detecta separadores es-AR: miles '.', decimales ','
            const normalized = numericText.replaceAll(".", "").replace(",", ".");
            const target = parseFloat(normalized);
            if (isNaN(target)) {
                return;
            }

            const decimals = (numericText.split(",")[1] || "").length;
            const duration = 900;
            const start = performance.now();

            const format = value => {
                const parts = value.toFixed(decimals).split(".");
                parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ".");
                return parts.length > 1 ? parts[0] + "," + parts[1] : parts[0];
            };

            const step = now => {
                const progress = Math.min((now - start) / duration, 1);
                const eased = 1 - Math.pow(1 - progress, 3);
                el.textContent = prefix + format(target * eased) + suffix;
                if (progress < 1) {
                    requestAnimationFrame(step);
                } else {
                    el.textContent = finalText;
                }
            };

            requestAnimationFrame(step);
        });
    }
};
