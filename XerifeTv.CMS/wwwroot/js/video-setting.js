(function (global, $) {
    if (!global) return;

    function uploadSubtitleFile(fileInputId, subtitleInputId, btnId) {
        const fileInput = document.getElementById(fileInputId);
        if (!fileInput || !fileInput.files || !fileInput.files[0]) return;

        const file = fileInput.files[0];
        const formData = new FormData();
        formData.append('file', file);

        const $btn = $(`#${btnId}`);
        $btn.attr('disabled', 'true');
        $('.loading-global .label').text('Processando upload, por favor aguarde...');
        $('.loading-global').show();

        fetch('/StorageFiles/UploadFile', {
            method: 'POST',
            body: formData
        })
            .then(response => response.json())
            .then(({ isSuccess, data }) => {
                if (isSuccess) $(`#${subtitleInputId}`).val(data);
            })
            .finally(() => {
                $btn.removeAttr('disabled');
                $('.loading-global').hide();
            });
    }

    function initVideoSetting(prefix) {
        if (!window.jQuery) {
            setTimeout(() => initVideoSetting(prefix), 50);
            return;
        }

        const $ = window.jQuery;
        const $videoUrl = $(`#${prefix}_videoUrl`);
        if ($videoUrl.length === 0) return;

        const $form = $videoUrl.closest('form');
        const $streamFormat = $(`#${prefix}_videoStreamFormat`);
        const $catalogByImdb = $(`#${prefix}_catalogByImdb`);

        function normalizeImdbId(value) {
            const imdbId = String(value || '').trim();
            return /^\d+$/.test(imdbId) ? `tt${imdbId}` : imdbId;
        }

        const $catalogProvider = $(`#${prefix}_catalogProvider`);

        function getCatalogUrl() {
            if ($catalogByImdb.length === 0 || !$catalogByImdb.prop('checked')) return null;

            const contentType = String($catalogByImdb.data('catalog-content-type') || '').toLowerCase();
            // base vem do provedor selecionado; se não houver dropdown, cai no data-attr (appsettings)
            const providerBase = $catalogProvider.length > 0 && $catalogProvider.val()
                ? String($catalogProvider.val())
                : String($catalogByImdb.data('catalog-base-url') || '');
            const baseUrl = providerBase.replace(/\/$/, '');

            // tipo de ID do provedor selecionado (imdb | tmdb)
            const selectedOption = $catalogProvider.length > 0 ? $catalogProvider.find('option:selected') : null;
            const idType = selectedOption && selectedOption.data('id-type')
                ? String(selectedOption.data('id-type')).toLowerCase()
                : 'imdb';

            if (contentType === 'movie') {
                // Provedor TMDB (ex: gaiaflix): usa o id do TMDB e o endpoint por query string.
                if (idType === 'tmdb') {
                    const tmdbId = String($('#tmdbId').val() || '').trim();
                    if (!tmdbId) return null;
                    return `${baseUrl}/api/gaiaflix-movie-source?id=${tmdbId}`;
                }

                const imdbId = normalizeImdbId($('#imdbId').val());
                if (!imdbId) return null;
                return `${baseUrl}/stream/movie/${imdbId}.json`;
            }

            if (contentType === 'series') {
                // Provedores TMDB (gaiaflix) são só filme - sem link de série.
                if (idType === 'tmdb') return null;

                const imdbId = normalizeImdbId($catalogByImdb.data('catalog-imdb-id'));
                if (!imdbId) return null;

                const episodeId = String($catalogByImdb.data('catalog-episode-id') || '');
                const season = $(`#season-${episodeId}`).val() || $catalogByImdb.data('catalog-season');
                const episode = $(`#number-${episodeId}`).val() || $catalogByImdb.data('catalog-episode');
                if (!season || !episode) return null;

                return `${baseUrl}/stream/series/${imdbId}:${season}:${episode}.json`;
            }

            return null;
        }

        function updateCatalogUrl() {
            const catalogUrl = getCatalogUrl();
            if (!catalogUrl) return;

            $videoUrl.val(catalogUrl);
            $streamFormat.val('mp4').prop('required', false);
        }

        $catalogByImdb.off(`change.vs_${prefix}`).on(`change.vs_${prefix}`, function () {
            updateCatalogUrl();
        });

        $catalogProvider.off(`change.vs_${prefix}`).on(`change.vs_${prefix}`, function () {
            updateCatalogUrl();
        });

        $('#imdbId').off(`change.vs_${prefix}`).on(`change.vs_${prefix}`, function () {
            updateCatalogUrl();
        });

        const episodeId = String($catalogByImdb.data('catalog-episode-id') || '');
        $(`#season-${episodeId}, #number-${episodeId}`).off(`change.vs_${prefix}`).on(`change.vs_${prefix}`, function () {
            updateCatalogUrl();
        });

        $(`#${prefix}_btn_subtitle_file`).off(`click.vs`).on('click.vs', function () {
            $(`#${prefix}_subtitle_file`).click();
        });

        $(`#${prefix}_subtitle_file`).off(`change.vs`).on('change.vs', function () {
            uploadSubtitleFile(`${prefix}_subtitle_file`, `${prefix}_videoSubtitle`, `${prefix}_btn_subtitle_file`);
        });

        $(`#${prefix}_videoSourceType`).off(`change.vs_${prefix}`).on(`change.vs_${prefix}`, function () {
            const $this = $(this);
            const $thisForm = $this.closest('form');
            if ($thisForm.find(`#${prefix}_videoUrl`).length === 0) return;

            const value = $this.val();
            if (value === 'delivery') {
                $(`#${prefix}_video-direct-section`).addClass('d-none');
                $(`#${prefix}_video-delivery-section`).removeClass('d-none');

                $(`#${prefix}_videoUrl`).val('');
                $(`#${prefix}_videoStreamFormat`).val('');

                $(`#${prefix}_mediaDeliveryProfileId`).prop('required', true);
                $(`#${prefix}_mediaRoute`).prop('required', true);
                $(`#${prefix}_videoUrl`).prop('required', false);
                $(`#${prefix}_videoStreamFormat`).prop('required', false);
                $catalogByImdb.prop('checked', false).prop('disabled', true);
            } else {
                $(`#${prefix}_video-direct-section`).removeClass('d-none');
                $(`#${prefix}_video-delivery-section`).addClass('d-none');

                $(`#${prefix}_mediaDeliveryProfileId`).val('');
                $(`#${prefix}_mediaRoute`).val('');

                // Link dublado não é obrigatório (pode ter só legendado, ou catálogo)
                $(`#${prefix}_videoUrl`).prop('required', false);
                $(`#${prefix}_videoStreamFormat`).prop('required', false);
                $(`#${prefix}_mediaDeliveryProfileId`).prop('required', false);
                $(`#${prefix}_mediaRoute`).prop('required', false);
                $catalogByImdb.prop('disabled', false);
            }
        });

        if ($(`#${prefix}_videoSourceType`).val() === 'delivery') {
            $catalogByImdb.prop('disabled', true);
        }
    }

    global.initVideoSetting = initVideoSetting;

})(window, window.jQuery);
