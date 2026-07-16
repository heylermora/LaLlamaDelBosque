(() => {
    const operationsPage = document.querySelector('.operations-page');
    if (!operationsPage) return;

    const formatMoney = value => new Intl.NumberFormat('es-CR', {
        style: 'currency',
        currency: 'CRC',
        maximumFractionDigits: 2
    }).format(Number(value) || 0);

    const sumInputs = selector => [...document.querySelectorAll(selector)]
        .reduce((total, input) => total + (Number(input.value) || 0), 0);

    function recalculateSummary() {
        const sinpeTotal = sumInputs('#sinpe-rows .money-input');
        const cardTotal = sumInputs('#card-rows .money-input');
        const providerTotal = sumInputs('.provider-amount');
        const initialCash = Number(document.querySelector('[name=initialCash]').value) || 0;
        const cashReceived = Number(document.querySelector('[name=cashReceived]').value) || 0;
        const prizePayments = Number(document.querySelector('[name=prizePayments]').value) || 0;
        const finalCash = Number(document.querySelector('[name=finalCash]').value) || 0;
        const expectedCash = initialCash + cashReceived - providerTotal - prizePayments;

        document.querySelector('#sinpe-total').textContent = formatMoney(sinpeTotal);
        document.querySelector('#card-total').textContent = formatMoney(cardTotal);
        document.querySelector('#summary-sinpe').textContent = formatMoney(sinpeTotal);
        document.querySelector('#summary-card').textContent = formatMoney(cardTotal);
        document.querySelector('#summary-grand').textContent = formatMoney(sinpeTotal + cardTotal);
        document.querySelector('#summary-providers').textContent = formatMoney(providerTotal);
        document.querySelector('#summary-expected').textContent = formatMoney(expectedCash);
        document.querySelector('#summary-difference').textContent = formatMoney(finalCash - expectedCash);
    }

    function createPaymentRow(prefix) {
        const row = document.createElement('tr');
        const key = `new-${Date.now()}-${crypto.randomUUID()}`;
        row.innerHTML = `
            <td>
                <input type="hidden" name="${prefix}Ids" value="${key}">
                <input class="form-control" name="${prefix}Titles" placeholder="Nombre o referencia" required>
            </td>
            <td>
                <input class="form-control money-input" name="${prefix}Amounts" type="number"
                       min="0" step="0.01" inputmode="decimal" required>
            </td>
            <td class="text-center">
                <input class="form-check-input" name="${prefix}Verified" value="${key}" type="checkbox">
            </td>
            <td>
                <button type="button" class="btn btn-sm btn-outline-danger remove-row" title="Eliminar fila">
                    <span class="material-icons">delete</span>
                </button>
            </td>`;
        return row;
    }

    function createProviderRow() {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>
                <input name="providerNames" class="form-control" placeholder="Nombre del proveedor" required>
            </td>
            <td>
                <input name="providerAmounts" class="form-control provider-amount" type="number"
                       min="0" step="0.01" required>
            </td>
            <td>
                <button type="button" class="btn btn-sm btn-outline-danger remove-row" title="Eliminar proveedor">
                    <span class="material-icons">delete</span>
                </button>
            </td>`;
        return row;
    }

    document.querySelectorAll('.add-payment').forEach(button => {
        button.addEventListener('click', () => {
            document.querySelector(`#${button.dataset.prefix}-rows`)
                .append(createPaymentRow(button.dataset.prefix));
            recalculateSummary();
        });
    });

    document.querySelector('#add-provider').addEventListener('click', () => {
        document.querySelector('#provider-rows').append(createProviderRow());
    });

    document.addEventListener('click', event => {
        const removeButton = event.target.closest('.remove-row');
        if (!removeButton) return;

        removeButton.closest('tr').remove();
        recalculateSummary();
    });

    document.addEventListener('input', event => {
        if (event.target.matches('.money-input, .provider-amount, .close-money')) {
            recalculateSummary();
        }
    });

    recalculateSummary();
})();
