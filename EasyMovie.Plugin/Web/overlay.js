(() => {
    const log = (...args) => console.info('[EasyMovie]', ...args);
    const warn = (...args) => console.warn('[EasyMovie]', ...args);

    const state = { installed: false, activeItems: new Set() };

    const install = () => {
        if (state.installed) return true;
        const api = window.ApiClient;
        if (!api || typeof api.ajax !== 'function') return false;

        // Intercept ApiClient.ajax — used by Jellyfin Web's getJSON/getIntros
        const origAjax = api.ajax.bind(api);
        api.ajax = function (options) {
            const url = options?.url || '';
            if (url.indexOf('EasyMoviePreroll/Decision') !== -1) {
                return origAjax(options);
            }
            const itemId = getIntroItemId(url);
            if (!itemId || state.activeItems.has(itemId)) {
                return origAjax(options);
            }

            state.activeItems.add(itemId);
            log('intercepted Intros request via ApiClient.ajax, itemId=', itemId);
            return origAjax(options).then(async (response) => {
                try {
                    const decision = await getDecision(itemId);
                    const strategy = decision.strategy || decision.Strategy;
                    const introItemId = decision.introItemId || decision.IntroItemId;
                    log('decision:', JSON.stringify(decision), 'strategy=', strategy, 'introItemId=', introItemId);

                    if (strategy === 'overlay' && introItemId) {
                        const completed = await playOverlay(introItemId);
                        log('overlay finished, completed=', completed);
                        if (completed) {
                            return { Items: [], TotalRecordCount: 0, items: [], totalRecordCount: 0 };
                        }
                    }
                    return response;
                } catch (error) {
                    warn('overlay failed, falling back to native intro', error);
                    return response;
                } finally {
                    state.activeItems.delete(itemId);
                }
            });
        };

        // Also intercept fetch — Jellyfin Web 10.11.11 SDK uses fetch directly
        const origFetch = window.fetch.bind(window);
        window.fetch = async function (input, init) {
            const response = await origFetch(input, init);
            const itemId = getIntroItemId(input);
            if (!itemId || state.activeItems.has(itemId)) return response;

            const decision = await getDecision(itemId);
            const strategy = decision.strategy || decision.Strategy;
            const introItemId = decision.introItemId || decision.IntroItemId;
            if (strategy !== 'overlay' || !introItemId) return response;

            state.activeItems.add(itemId);
            log('intercepted Intros request via fetch, itemId=', itemId);
            try {
                const completed = await playOverlay(introItemId);
                log('overlay finished, completed=', completed);
                return completed ? emptyIntrosResponse() : response;
            } catch (error) {
                warn('overlay failed, falling back to native intro', error);
                return response;
            } finally {
                state.activeItems.delete(itemId);
            }
        };

        state.installed = true;
        log('overlay installed (API interception: ApiClient.ajax + fetch)');
        return true;
    };

    const getIntroItemId = input => {
        const url = typeof input === 'string' ? input : input?.url;
        if (!url) return null;
        const match = url.match(/\/Items\/([^/?]+)\/Intros(?:\?|$)/i);
        return match?.[1];
    };

    const getDecision = async itemId => {
        const api = window.ApiClient;
        const url = api.getUrl('EasyMoviePreroll/Decision', { itemId });
        return await api.ajax({ type: 'GET', url, dataType: 'json' });
    };

    const emptyIntrosResponse = () => new Response(
        JSON.stringify({ Items: [], TotalRecordCount: 0, items: [], totalRecordCount: 0 }),
        { status: 200, statusText: 'OK', headers: { 'Content-Type': 'application/json' } }
    );

    const playOverlay = introItemId => new Promise(resolve => {
        const api = window.ApiClient;
        const overlay = document.createElement('div');
        const video = document.createElement('video');
        const skip = document.createElement('button');
        let finished = false;

        const finish = completed => {
            if (finished) return;
            finished = true;
            window.clearTimeout(timeout);
            video.pause();
            overlay.remove();
            resolve(completed);
        };
        const timeout = window.setTimeout(() => { warn('overlay timed out (120s)'); finish(false); }, 120000);

        overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;background:#000;';
        video.style.cssText = 'width:100%;height:100%;object-fit:contain;';
        video.dataset.easymovieOverlay = 'true';
        video.autoplay = true;
        video.playsInline = true;
        video.controls = false;
        video.src = api.getUrl(`Videos/${introItemId}/stream`, { static: 'true', api_key: api.accessToken() });
        video.addEventListener('ended', () => { log('overlay video ended'); finish(true); }, { once: true });
        video.addEventListener('error', (e) => { warn('overlay video error:', e, 'code:', video.error?.code); finish(false); }, { once: true });
        skip.type = 'button';
        skip.textContent = 'Omitir intro';
        skip.style.cssText = 'position:absolute;right:2rem;bottom:2rem;border:0;border-radius:.25rem;padding:.75rem 1rem;background:#fff;color:#000;font-size:1rem;cursor:pointer;';
        skip.addEventListener('click', () => { log('skip button clicked'); finish(true); }, { once: true });
        overlay.append(video, skip);
        document.body.appendChild(overlay);
        log('overlay appended to DOM, calling video.play()...');

        const playPromise = video.play();
        if (playPromise && typeof playPromise.then === 'function') {
            playPromise
                .then(() => log('overlay video.play() succeeded'))
                .catch((err) => { warn('overlay video.play() rejected:', err?.name, err?.message); finish(false); });
        } else {
            log('overlay video.play() returned non-promise');
        }
    });

    if (!install()) {
        log('install failed, retrying...');
        const retry = window.setInterval(() => {
            if (install()) { log('install succeeded on retry'); window.clearInterval(retry); }
        }, 250);
        window.setTimeout(() => { window.clearInterval(retry); log('install retry gave up after 10s'); }, 10000);
    }
})();
