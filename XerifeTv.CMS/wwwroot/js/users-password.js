(() => {
    const modal = document.getElementById('changePasswordModal');
    const form = document.getElementById('changePasswordForm');
    const message = document.getElementById('changePasswordMessage');
    const password = form.elements.NewPassword;
    const confirmation = form.elements.ConfirmPassword;
    const submit = form.querySelector('[type="submit"]');
    let saving = false;

    modal.addEventListener('show.bs.modal', event => {
        form.reset();
        confirmation.setCustomValidity('');
        message.textContent = '';
        submit.disabled = false;
        form.action = event.relatedTarget.dataset.action;
        form.elements.Id.value = event.relatedTarget.dataset.userId;
        document.getElementById('changePasswordUser').textContent = event.relatedTarget.dataset.userName;
    });
    modal.addEventListener('shown.bs.modal', () => password.focus());
    modal.addEventListener('hide.bs.modal', event => {
        if (saving) event.preventDefault();
    });
    modal.addEventListener('hidden.bs.modal', () => form.reset());
    for (const input of [password, confirmation]) {
        input.addEventListener('input', () => confirmation.setCustomValidity(''));
    }
    form.addEventListener('submit', async event => {
        event.preventDefault();
        event.stopImmediatePropagation();
        if (saving) return;
        confirmation.setCustomValidity(password.value === confirmation.value ? '' : 'As senhas não coincidem.');
        if (!form.reportValidity()) return;
        const body = new FormData(form);
        saving = true;
        for (const control of form.querySelectorAll('button, input:not([type="hidden"])')) control.disabled = true;
        submit.textContent = 'Salvando...';
        message.textContent = '';
        let succeeded = false;
        try {
            const response = await fetch(form.action, { method: 'POST', body });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || 'Não foi possível trocar a senha.');
            password.value = '';
            confirmation.value = '';
            message.className = 'mb-0 text-success';
            message.textContent = data.message;
            succeeded = true;
        } catch (error) {
            message.className = 'mb-0 text-danger';
            message.textContent = error instanceof SyntaxError ? 'Sua sessão pode ter expirado. Atualize a página e tente novamente.' : error.message;
        } finally {
            saving = false;
            for (const control of form.querySelectorAll('button, input:not([type="hidden"])')) control.disabled = false;
            submit.disabled = succeeded;
            submit.textContent = 'Salvar senha';
        }
    }, { capture: true });
})();
