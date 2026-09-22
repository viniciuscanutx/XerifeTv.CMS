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
            let imdbId = $catalogByImdb.data('catalog-imdb-id');

            if (contentType === 'movie') {
                imdbId = $('#imdbId').val();
            }

            imdbId = normalizeImdbId(imdbId);
            if (!imdbId) return null;

            if (contentType === 'movie') {
                return `${baseUrl}/stream/movie/${imdbId}.json`.replace(/^\/stream/, 'stream');
            }

            if (contentType === 'series') {
                const episodeId = String($catalogByImdb.data('catalog-episode-id') || '');
                const season = $(`#season-${episodeId}`).val() || $catalogByImdb.data('catalog-season');
                const episode = $(`#number-${episodeId}`).val() || $catalogByImdb.data('catalog-episode');
                if (!season || !episode) return null;

                return `${baseUrl}/stream/series/${imdbId}:${season}:${episode}.json`.replace(/^\/stream/, 'stream');
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

                $(`#${prefix}_videoUrl`).prop('required', true);
                $(`#${prefix}_videoStreamFormat`).prop('required', !$catalogByImdb.prop('checked'));
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
