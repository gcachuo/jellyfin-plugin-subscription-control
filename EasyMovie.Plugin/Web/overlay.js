(() => {
    const state = {
        installed: false,
        activeItems: new Set()
    };

    const install = () => {
        const api = window.ApiClient;
        if (!api || typeof api.getIntros !== 'function' || state.installed) {
            return false;
        }

        const getIntros = api.getIntros.bind(api);
        api.getIntros = async itemId => {
            const intros = await getIntros(itemId);
            const decision = await getDecision(api, itemId);
            if (decision.strategy !== 'overlay' || !decision.introItemId || state.activeItems.has(itemId)) {
                return intros;
            }

            state.activeItems.add(itemId);
            try {
                await playOverlay(api, decision.introItemId);
                return emptyIntros(intros);
            } finally {
                state.activeItems.delete(itemId);
            }
        };
        state.installed = true;
        return true;
    };

    const emptyIntros = intros => Array.isArray(intros)
        ? []
        : {
            ...intros,
            Items: [],
            TotalRecordCount: 0,
            items: [],
            totalRecordCount: 0
        };

    const getDecision = async (api, itemId) => {
        try {
            const url = api.getUrl('EasyMoviePreroll/Decision', { itemId });
            return await api.ajax({ type: 'GET', url, dataType: 'json' });
        } catch (error) {
            console.warn('EasyMovie: unable to load preroll decision', error);
            return { strategy: 'none' };
        }
    };

    const playOverlay = (api, introItemId) => new Promise(resolve => {
        const overlay = document.createElement('div');
        const video = document.createElement('video');
        const skip = document.createElement('button');
        let finished = false;
        const finish = () => {
            if (finished) {
                return;
            }

            finished = true;
            window.clearTimeout(timeout);
            video.pause();
            overlay.remove();
            resolve();
        };
        const timeout = window.setTimeout(finish, 120000);

        overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;background:#000;';
        video.style.cssText = 'width:100%;height:100%;object-fit:contain;';
        video.autoplay = true;
        video.playsInline = true;
        video.controls = false;
        video.src = api.getUrl(`Videos/${introItemId}/stream`, {
            static: 'true',
            api_key: api.accessToken()
        });
        video.addEventListener('ended', finish, { once: true });
        video.addEventListener('error', finish, { once: true });
        skip.type = 'button';
        skip.textContent = 'Omitir intro';
        skip.style.cssText = 'position:absolute;right:2rem;bottom:2rem;border:0;border-radius:.25rem;padding:.75rem 1rem;background:#fff;color:#000;font-size:1rem;cursor:pointer;';
        skip.addEventListener('click', finish, { once: true });
        overlay.append(video, skip);
        document.body.appendChild(overlay);
        video.play().catch(finish);
    });

    if (!install()) {
        const retry = window.setInterval(() => {
            if (install()) {
                window.clearInterval(retry);
            }
        }, 250);
        window.setTimeout(() => window.clearInterval(retry), 10000);
    }
})();
