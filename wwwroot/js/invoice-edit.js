/**
 * Shared JavaScript logic for Sales and Purchase Invoice Editing
 */
(function (window) {
    'use strict';

    window.initInvoiceEdit = function (config) {
        const products = config.products || [];
        const existingItems = config.existingItems || [];
        const itemIdFieldName = config.itemIdFieldName || 'ItemId';

        let rowIndex = 0;

        function buildProductOptions(selectedProductId) {
            let opts = '<option value="">-- Select Product --</option>';
            products.forEach(p => {
                const val = p.Value ?? p.value;
                const txt = p.Text  ?? p.text;
                const sel = (val == selectedProductId) ? ' selected' : '';
                opts += `<option value="${val}"${sel}>${txt}</option>`;
            });
            return opts;
        }

        function addRow(itemId, productId, quantity, unitPrice) {
            const tbody = document.getElementById('itemsBody');
            if (!tbody) return;

            const idx = rowIndex++;
            itemId    = itemId    ?? 0;
            productId = productId ?? '';
            quantity  = quantity  ?? 1;
            unitPrice = unitPrice ?? '0.00';

            const sub = parseFloat(quantity) * parseFloat(unitPrice);

            const tr = document.createElement('tr');
            tr.setAttribute('data-row', idx);
            tr.innerHTML = `
                <td class="ps-4">
                    <input type="hidden"
                           name="LineItems[${idx}].${itemIdFieldName}"
                           class="item-id-input"
                           value="${itemId}" />
                    <select name="LineItems[${idx}].ProductId"
                            class="form-select form-select-sm product-select"
                            onchange="window.invoiceEditApp.updateRowTotal(${idx})" required>
                        ${buildProductOptions(productId)}
                    </select>
                </td>
                <td>
                    <input type="number" name="LineItems[${idx}].Quantity"
                           class="form-control form-control-sm qty-input"
                           min="1" value="${quantity}"
                           oninput="window.invoiceEditApp.updateRowTotal(${idx})" required />
                </td>
                <td>
                    <input type="number" name="LineItems[${idx}].UnitPrice"
                           class="form-control form-control-sm price-input"
                           min="0" step="0.01" value="${parseFloat(unitPrice).toFixed(2)}"
                           oninput="window.invoiceEditApp.updateRowTotal(${idx})" required />
                </td>
                <td class="subtotal fw-semibold text-secondary">${formatCurrency(sub)}</td>
                <td>
                    <button type="button" class="btn btn-sm btn-outline-danger"
                            onclick="window.invoiceEditApp.removeRow(this)" title="Remove row">
                        <i class="bi bi-trash"></i>
                    </button>
                </td>`;
            tbody.appendChild(tr);
            updateNoRowsMsg();
            updateGrandTotal();
        }

        function addNewRow() {
            addRow(0, '', 1, '0.00');
        }

        function removeRow(btn) {
            btn.closest('tr').remove();
            reindexRows();
            updateNoRowsMsg();
            updateGrandTotal();
        }

        function reindexRows() {
            const rows = document.querySelectorAll('#itemsBody tr');
            rows.forEach((tr, newIdx) => {
                tr.setAttribute('data-row', newIdx);

                const itemIdInput = tr.querySelector('.item-id-input');
                if (itemIdInput) itemIdInput.name = `LineItems[${newIdx}].${itemIdFieldName}`;

                const select = tr.querySelector('.product-select');
                if (select) select.name = `LineItems[${newIdx}].ProductId`;

                const qty = tr.querySelector('.qty-input');
                if (qty) qty.name = `LineItems[${newIdx}].Quantity`;

                const price = tr.querySelector('.price-input');
                if (price) price.name = `LineItems[${newIdx}].UnitPrice`;

                if (select) select.setAttribute('onchange', `window.invoiceEditApp.updateRowTotal(${newIdx})`);
                if (qty) qty.setAttribute('oninput', `window.invoiceEditApp.updateRowTotal(${newIdx})`);
                if (price) price.setAttribute('oninput', `window.invoiceEditApp.updateRowTotal(${newIdx})`);
            });
            rowIndex = rows.length;
        }

        function updateRowTotal(idx) {
            const tr = document.querySelector(`tr[data-row="${idx}"]`);
            if (!tr) return;
            const qty   = parseFloat(tr.querySelector('.qty-input').value)   || 0;
            const price = parseFloat(tr.querySelector('.price-input').value) || 0;
            tr.querySelector('.subtotal').textContent = formatCurrency(qty * price);
            updateGrandTotal();
        }

        function updateGrandTotal() {
            let total = 0;
            document.querySelectorAll('.subtotal').forEach(cell => {
                total += parseCurrency(cell.textContent);
            });

            const paidInput = document.getElementById('paidAmountInput');
            const remainingInput = document.getElementById('remainingAmountInput');
            const changedAmountEl = document.getElementById('changedAmount');

            const paid = paidInput ? parseFloat(paidInput.value) || 0 : 0;
            const prevRemaining = remainingInput ? parseFloat(remainingInput.value) || 0 : 0;

            // changedAmount is the difference between current calculated total and original invoice total
            const originalTotal = parseFloat(document.getElementById('grandTotal').getAttribute('data-original') || '0') || 0;
            const newTotal = total;
            const changed = Math.round((newTotal - originalTotal) * 100) / 100;

            // Update hidden changedAmount input
            if (changedAmountEl) changedAmountEl.value = changed.toFixed(2);

            // Remaining amount = previous remaining + changed - paid
            const remaining = Math.max(0, prevRemaining + changed - paid);

            const grandTotalEl = document.getElementById('grandTotal');
            if (grandTotalEl) {
                grandTotalEl.textContent = formatCurrency(newTotal);
            }
            if (remainingInput) remainingInput.value = remaining.toFixed(2);
        }

        function updateNoRowsMsg() {
            const body = document.getElementById('itemsBody');
            const msg  = document.getElementById('noRowsMsg');
            if (!body || !msg) return;
            msg.style.display = body.rows.length > 0 ? 'none' : 'block';
        }

        function formatCurrency(value) {
            return '$' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        }

        function parseCurrency(str) {
            return parseFloat(str.replace(/[$,]/g, '')) || 0;
        }

        // Form validation
        const invoiceForm = document.getElementById('invoiceForm');
        if (invoiceForm) {
            invoiceForm.addEventListener('submit', function (e) {
                const body = document.getElementById('itemsBody');
                if (!body || body.rows.length === 0) {
                    e.preventDefault();
                    const summary = document.getElementById('validationSummary');
                    if (summary) {
                        summary.style.display = 'block';
                        summary.textContent = 'Please add at least one product line item before saving.';
                    }
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                }
            });
        }

        window.invoiceEditApp = {
            addNewRow: addNewRow,
            removeRow: removeRow,
            updateRowTotal: updateRowTotal
        };

        // Render existing rows on load
        if (existingItems && existingItems.length > 0) {
            existingItems.forEach(item => {
                addRow(item.itemId, item.productId, item.quantity, item.unitPrice);
            });
        } else {
            addRow(0);
        }

        // Store original total on grandTotal element for change tracking
        const grandTotalEl = document.getElementById('grandTotal');
        if (grandTotalEl) {
            let orig = 0;
            if (existingItems && existingItems.length > 0) {
                existingItems.forEach(it => {
                    orig += (parseFloat(it.quantity) || 0) * (parseFloat(it.unitPrice) || 0);
                });
            }
            grandTotalEl.setAttribute('data-original', orig.toFixed(2));
            grandTotalEl.textContent = formatCurrency(orig);
        }

        // Attach input handler to paid amount input to recalc remaining when paid changes
        const paidInput = document.getElementById('paidAmountInput');
        if (paidInput) {
            paidInput.addEventListener('input', function () {
                updateGrandTotal();
            });
        }
    };
})(window);
