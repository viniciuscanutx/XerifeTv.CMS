// upload excel file 
$(document).on('change', '.importExcelFile', function () {
  const modal = $(this).closest('.modal');
  const file = $(this).prop('files')[0];
  if (!file) return;

  modal.find('.file-uploaded-name i').addClass('fa-solid fa-file-excel');
  modal.find('.file-uploaded-name span').text(file.name);
  modal.find('.btn-excel-file-submit').prop('disabled', false);
});

// when closing the modal reset form
$(document).on('hidden.bs.modal', '.modal', function () {
  const modal = $(this);
  modal.find('.importExcelFile').val('');
  modal.find('.file-uploaded-name i').removeClass('fa-solid fa-file-excel');
  modal.find('.file-uploaded-name span').text('');
  modal.find('.btn-excel-file-submit').text('Cadastrar').prop('disabled', true);
  modal.find('.select-file-container').show();
  modal.find('.process-file-container').hide();
  modal.find('.finish-process-container').hide();
  modal.find('.btn-close').show();
});

// submit spreadsheet
$(document).on('click', '.btn-excel-file-submit', async function () {
  if (!confirm('Confirmar ação?')) return;

  const btn = $(this);
  const modal = btn.closest('.modal');
  const fileInput = modal.find('.importExcelFile')[0];
  const file = fileInput ? fileInput.files[0] : null;
  if (!file) return;

  const formData = new FormData();
  formData.append('file', file);

  const controller = btn.data('controller');
  const action = btn.data('action');
  const actionMonitorProgress = btn.data('monitorProgressAction');

  var monitorProgressInterval = 0;

  try {
    btn.text('Processando...').prop('disabled', true);
    modal.find('.isBackgroundJob').prop('disabled', true);

    if (modal.find('.isBackgroundJob').is(':checked')) {
      const formDataBackgroundJob = new FormData();
      formDataBackgroundJob.append('spreadsheetFile', file);

      const controllerType = controller.toUpperCase();
      const backgroundJobTypes = {
        SERIES: 'REGISTER_SPREADSHEET_SERIES',
        CHANNELS: 'REGISTER_SPREADSHEET_CHANNELS',
        MOVIES: 'REGISTER_SPREADSHEET_MOVIES',
      };

      const backgroundJobType = backgroundJobTypes[controllerType] || 'REGISTER_SPREADSHEET_MOVIES';

      formDataBackgroundJob.append('type', backgroundJobType);

      await fetch(`/BackgroundJobQueue/AddJobInQueueSpreadsheetRegisters`, {
        method: 'POST',
        body: formDataBackgroundJob
      });

      location.replace(`/${controller}`);
      return;
    }

    modal.find('.isBackgroundJob').parent().hide();
    modal.find('.select-file-container').hide();
    modal.find('.btn-close').hide();
    modal.find('.process-file-container').show();

    // submit file
    const response = await fetch(`/${controller}/${action}`, {
      method: 'POST',
      body: formData
    });

    const importId = await response.text();

    // monitor progress records
    monitorProgressInterval = setInterval(async () => {

      var monitorResponse = await fetch(`/${controller}/${actionMonitorProgress}?importId=${importId}`);
      const { successCount, failCount, errorList, progressCount } = await monitorResponse.json();

      if (progressCount == 0) return;

      modal.find('.process .progress-bar').css('width', `${progressCount}%`);
      modal.find('.process span.status-percent').text(`${progressCount}%`);

      if (progressCount == 100) {
        clearInterval(monitorProgressInterval);

        modal.find('.finish-process-container .success-count').text(successCount);
        modal.find('.finish-process-container .fail-count').text(failCount);

        $(errorList).each((_, message) => {
          const errorItem = document.createElement('li');
          errorItem.textContent = message;
          errorItem.classList.add('list-group-item');
          modal.find('.finish-process-container .errorList .list-group').append(errorItem);
        });

        if (errorList.length > 0) modal.find('.finish-process-container .errorList').show();

        modal.find('.process .progress-bar').css('width', '100%');
        modal.find('.process span.status-percent').text('100%');
        modal.find('.process span.status-text').text('Processo de cadastros finalizado.');

        setTimeout(() => {
          modal.find('.process-file-container').hide();
          modal.find('.finish-process-container').show();

          btn.text('Pronto').prop('disabled', false);
          btn.off('click').on('click', () => location.replace(`/${controller}`));
        }, 1250);
      }

    }, 2500);
  }
  catch (error) {
    if (!error) return;
    const errorItem = document.createElement('li');
    errorItem.textContent = String(error);
    errorItem.classList.add('list-group-item');

    modal.find('.finish-process-container .errorList .list-group').append(errorItem);
    modal.find('.finish-process-container .errorList').show();

    clearInterval(monitorProgressInterval);
    modal.find('.process .progress-bar').css('width', '100%');
    modal.find('.process span.status-percent').text('100%');
    modal.find('.process span.status-text').text('Processo de cadastros finalizado.');

    setTimeout(() => {
      modal.find('.process-file-container').hide();
      modal.find('.finish-process-container').show();

      btn.text('Pronto').prop('disabled', false);
      btn.off('click').on('click', () => location.replace(`/${controller}`));
    }, 1250);
  }
});