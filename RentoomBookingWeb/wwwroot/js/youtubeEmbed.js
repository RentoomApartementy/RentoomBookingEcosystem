let iframeApiPromise;
const players = new Map();
const pendingPlayers = new Set();

function loadIframeApi() {
    if (window.YT?.Player) {
        return Promise.resolve(window.YT);
    }

    if (!iframeApiPromise) {
        iframeApiPromise = new Promise((resolve, reject) => {
            const previousReadyHandler = window.onYouTubeIframeAPIReady;
            window.onYouTubeIframeAPIReady = () => {
                previousReadyHandler?.();
                resolve(window.YT);
            };

            const script = document.createElement('script');
            script.src = 'https://www.youtube.com/iframe_api';
            script.async = true;
            script.onerror = () => reject(new Error('Failed to load the YouTube IFrame Player API.'));
            document.head.appendChild(script);
        });
    }

    return iframeApiPromise;
}

export async function initialize(elementId, volume) {
    pendingPlayers.add(elementId);
    const YT = await loadIframeApi();

    if (!pendingPlayers.has(elementId)) {
        return;
    }

    players.get(elementId)?.destroy();

    const player = new YT.Player(elementId, {
        events: {
            onReady: event => event.target.setVolume(Math.min(100, Math.max(0, volume)))
        }
    });

    players.set(elementId, player);
}

export function dispose(elementId) {
    pendingPlayers.delete(elementId);
    players.get(elementId)?.destroy();
    players.delete(elementId);
}
