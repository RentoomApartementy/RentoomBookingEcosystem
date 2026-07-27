let embedScriptPromise;

async function ensureInstagramEmbedScript() {
    if (window.instgrm?.Embeds?.process) {
        return;
    }

    if (!embedScriptPromise) {
        embedScriptPromise = new Promise((resolve, reject) => {
            const existing = document.querySelector('script[data-rentoom-instagram-embed="true"]');
            if (existing) {
                existing.addEventListener('load', () => resolve(), { once: true });
                existing.addEventListener('error', () => reject(new Error('Failed to load Instagram embed script.')), { once: true });
                return;
            }

            const script = document.createElement('script');
            script.src = 'https://www.instagram.com/embed.js';
            script.async = true;
            script.defer = true;
            script.setAttribute('data-rentoom-instagram-embed', 'true');
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('Failed to load Instagram embed script.'));
            document.head.appendChild(script);
        });
    }

    await embedScriptPromise;
}

export async function processEmbeds() {
    await ensureInstagramEmbedScript();

    if (window.instgrm?.Embeds?.process) {
        window.instgrm.Embeds.process();
    }
}
