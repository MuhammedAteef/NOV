/**
 * Shared JavaScript logic for Sales and Purchase Invoice Creation with Stripe Payment Integration & Stock Validation
 */
(function (window) {
    'use strict';

    window.initInvoiceForm = function (config) {
        const products = config.products || [];
        const stripeKey = config.stripeKey || '';
        const isSales = config.isSales || false;

        let stripe = null;
        let elements = null;
        let cardElement = null;
        let isCardMounted = false;
        let rowIndex = 0;

        function showCardError(msg) {
            const alertBox = document.getElementById('card-alert');
            const errorSpan = document.getElementById('card-error-msg');
            const cardEl = document.getElementById('card-element');
            if (alertBox && errorSpan) {
                errorSpan.textContent = msg;
                alertBox.style.display = 'flex';
            }
            if (cardEl) {
                cardEl.classList.add('border-danger');
            }
        }

        function clearCardError() {
            const alertBox = document.getElementById('card-alert');
            const errorSpan = document.getElementById('card-error-msg');
            const cardEl = document.getElementById('card-element');
            if (alertBox && errorSpan) {
                errorSpan.textContent = '';
                alertBox.style.display = 'none';
            }
            if (cardEl) {
                cardEl.classList.remove('border-danger');
            }
        }

        function mountStripeCard() {
            if (!stripeKey || typeof Stripe === 'undefined') return;
            const cardContainer = document.getElementById('stripeCardContainer');
            const targetEl = document.getElementById('card-element');
            if (!cardContainer || !targetEl) return;

            if (!stripe) {
                stripe = Stripe(stripeKey);
                elements = stripe.elements();
                cardElement = elements.create('card', {
                    style: {
                        base: {
                            fontSize: '16px',
                            color: '#0F172A',
                            fontFamily: 'system-ui, -apple-system, sans-serif',
                            '::placeholder': { color: '#94A3B8' }
                        },
                        invalid: { color: '#EF4444' }
                    }
                });
                cardElement.on('change', e => {
                    if (e.error) {
                        showCardError(e.error.message);
                    } else {
                        clearCardError();
                    }
                });
            }

            if (!isCardMounted && cardContainer.style.display !== 'none') {
                cardElement.mount('#card-element');
                isCardMounted = true;
            }
        }

        function togglePaymentDetails(val) {
            const isPaid = val === 'Cash' || val === 'Card';
            const isCard = val === 'Card';

            const refContainer = document.getElementById('referenceContainer');
            const cardContainer = document.getElementById('stripeCardContainer');
            const notesContainer = document.getElementById('notesContainer');

            if (refContainer) refContainer.style.display = (isPaid && !isCard) ? 'block' : 'none';
            if (cardContainer) {
                cardContainer.style.display = isCard ? 'block' : 'none';
                if (isCard) {
                    setTimeout(mountStripeCard, 50);
                }
            }
            if (notesContainer) notesContainer.style.display = isPaid ? 'block' : 'none';

            if (!isCard) clearCardError();
        }

        // Attach toggle function to select input change
        const methodSelect = document.getElementById('paymentMethodSelect');
        if (methodSelect) {
            methodSelect.addEventListener('change', function () {
                togglePaymentDetails(this.value);
            });
            togglePaymentDetails(methodSelect.value);
        }

        function buildProductOptions() {
            let opts = '<option value="" data-price="0" data-stock="0">-- Select Product --</option>';
            products.forEach(p => {
                const val = p.value ?? p.Value;
                const txt = p.text ?? p.Text;
                const price = p.sellingPrice ?? p.SellingPrice ?? 0;
                const stock = p.stockQuantity ?? p.StockQuantity ?? 0;
                opts += `<option value="${val}" data-price="${price}" data-stock="${stock}">${txt}</option>`;
            });
            return opts;
        }

        function validateQuantity(tr) {
            if (!isSales) return true;
            const select = tr.querySelector('.product-select');
            const qtyInput = tr.querySelector('.qty-input');
            const qtyErr = tr.querySelector('.qty-error');
            if (!qtyInput || !select || !select.value) return true;

            const selectedOpt = select.options[select.selectedIndex];
            const stock = parseInt(selectedOpt.getAttribute('data-stock'), 10) || 0;
            const qty = parseFloat(qtyInput.value) || 0;

            if (qty > stock) {
                qtyInput.classList.add('is-invalid');
                if (qtyErr) {
                    qtyErr.textContent = `Max available: ${stock}`;
                    qtyErr.style.display = 'block';
                }
                return false;
            } else {
                qtyInput.classList.remove('is-invalid');
                if (qtyErr) {
                    qtyErr.textContent = '';
                    qtyErr.style.display = 'none';
                }
                return true;
            }
        }

        function onProductSelectChange(idx) {
            const tr = document.querySelector(`tr[data-row="${idx}"]`);
            if (!tr) return;
            const select = tr.querySelector('.product-select');
            const selectedOpt = select.options[select.selectedIndex];
            const priceInput = tr.querySelector('.price-input');
            const stockBadge = tr.querySelector('.stock-hint');

            const price = parseFloat(selectedOpt.getAttribute('data-price')) || 0;
            const stock = parseInt(selectedOpt.getAttribute('data-stock'), 10) || 0;

            // Auto-fill Unit Price with SellingPrice for Sales Invoice
            if (isSales && priceInput && price > 0) {
                priceInput.value = price.toFixed(2);
            }

            // Display stock hint badge
            if (stockBadge) {
                if (select.value && isSales) {
                    stockBadge.textContent = `In Stock: ${stock}`;
                    stockBadge.className = `stock-hint badge ${stock > 0 ? 'bg-info-subtle text-dark' : 'bg-danger-subtle text-danger'} mt-1`;
                } else {
                    stockBadge.textContent = '';
                    stockBadge.className = 'stock-hint badge mt-1';
                }
            }

            validateQuantity(tr);
            updateRowTotal(idx);
        }

        function onQtyInput(idx) {
            const tr = document.querySelector(`tr[data-row="${idx}"]`);
            if (tr) {
                validateQuantity(tr);
                updateRowTotal(idx);
            }
        }

        function addRow() {
            const tbody = document.getElementById('itemsBody');
            if (!tbody) return;
            const idx = rowIndex++;

            const tr = document.createElement('tr');
            tr.setAttribute('data-row', idx);
            tr.innerHTML = `
                <td class="ps-4 align-top">
                    <select name="LineItems[${idx}].ProductId"
                            class="form-select form-select-sm product-select"
                            onchange="window.invoiceApp.onProductSelectChange(${idx})" required>
                        ${buildProductOptions()}
                    </select>
                    <div class="stock-hint badge mt-1"></div>
                </td>
                <td class="align-top">
                    <input type="number" name="LineItems[${idx}].Quantity"
                           class="form-control form-control-sm qty-input"
                           min="1" value="1"
                           oninput="window.invoiceApp.onQtyInput(${idx})" required />
                    <div class="qty-error text-danger small mt-1 fw-semibold" style="display:none;"></div>
                </td>
                <td class="align-top">
                    <input type="number" name="LineItems[${idx}].UnitPrice"
                           class="form-control form-control-sm price-input"
                           min="0" step="0.01" value="0.00"
                           oninput="window.invoiceApp.updateRowTotal(${idx})" required />
                </td>
                <td class="subtotal fw-semibold text-secondary align-top pt-2">$0.00</td>
                <td class="align-top">
                    <button type="button" class="btn btn-sm btn-outline-danger"
                            onclick="window.invoiceApp.removeRow(this)" title="Remove row">
                        <i class="bi bi-trash"></i>
                    </button>
                </td>`;
            tbody.appendChild(tr);
            updateNoRowsMsg();
            updateGrandTotal();
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

                const select = tr.querySelector('.product-select');
                if (select) {
                    select.name = `LineItems[${newIdx}].ProductId`;
                    select.setAttribute('onchange', `window.invoiceApp.onProductSelectChange(${newIdx})`);
                }

                const qty = tr.querySelector('.qty-input');
                if (qty) {
                    qty.name = `LineItems[${newIdx}].Quantity`;
                    qty.setAttribute('oninput', `window.invoiceApp.onQtyInput(${newIdx})`);
                }

                const price = tr.querySelector('.price-input');
                if (price) {
                    price.name = `LineItems[${newIdx}].UnitPrice`;
                    price.setAttribute('oninput', `window.invoiceApp.updateRowTotal(${newIdx})`);
                }
            });

            rowIndex = rows.length;
        }

        function updateRowTotal(idx) {
            const tr = document.querySelector(`tr[data-row="${idx}"]`);
            if (!tr) return;
            const qty   = parseFloat(tr.querySelector('.qty-input').value)   || 0;
            const price = parseFloat(tr.querySelector('.price-input').value) || 0;
            const sub   = qty * price;
            tr.querySelector('.subtotal').textContent = formatCurrency(sub);
            updateGrandTotal();
        }

        function updateGrandTotal() {
            let total = 0;
            document.querySelectorAll('.subtotal').forEach(cell => {
                total += parseCurrency(cell.textContent);
            });
            const grandTotalEl = document.getElementById('grandTotal');
            if (grandTotalEl) grandTotalEl.textContent = formatCurrency(total);
        }

        function updateNoRowsMsg() {
            const body = document.getElementById('itemsBody');
            const msg = document.getElementById('noRowsMsg');
            if (!body || !msg) return;
            msg.style.display = body.rows.length > 0 ? 'none' : 'block';
        }

        function formatCurrency(value) {
            return '$' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        }

        function parseCurrency(str) {
            return parseFloat(str.replace(/[$,]/g, '')) || 0;
        }

        // Form submission handling
        const invoiceForm = document.getElementById('invoiceForm');
        if (invoiceForm) {
            invoiceForm.addEventListener('submit', async function (e) {
                clearCardError();
                const body = document.getElementById('itemsBody');
                const rows = body ? body.rows.length : 0;

                if (rows === 0) {
                    e.preventDefault();
                    const summary = document.getElementById('validationSummary');
                    if (summary) {
                        summary.style.display = 'block';
                        summary.textContent = 'Please add at least one product line item before saving.';
                    }
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                    return;
                }

                // Check stock errors for sales invoices
                if (isSales) {
                    let hasStockError = false;
                    document.querySelectorAll('#itemsBody tr').forEach(tr => {
                        if (!validateQuantity(tr)) {
                            hasStockError = true;
                        }
                    });

                    if (hasStockError) {
                        e.preventDefault();
                        const summary = document.getElementById('validationSummary');
                        if (summary) {
                            summary.style.display = 'block';
                            summary.textContent = 'One or more line items exceed available stock quantity. Please adjust quantities before saving.';
                        }
                        window.scrollTo({ top: 0, behavior: 'smooth' });
                        return;
                    }
                }

                const paymentMethod = methodSelect ? methodSelect.value : 'Unpaid';
                const intentInput = document.getElementById('stripePaymentIntentId');

                if (paymentMethod === 'Card' && intentInput && !intentInput.value) {
                    e.preventDefault();

                    const btn = document.getElementById('saveInvoiceBtn');
                    if (btn) {
                        btn.disabled = true;
                        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing Card Payment…';
                    }

                    const grandTotalEl = document.getElementById('grandTotal');
                    const totalAmount = parseCurrency(grandTotalEl ? grandTotalEl.textContent : '0');
                    if (!totalAmount || totalAmount <= 0) {
                        showCardError('Invoice total must be greater than zero for card payment.');
                        if (btn) {
                            btn.disabled = false;
                            btn.innerHTML = '<i class="bi bi-save me-1"></i> Save Invoice';
                        }
                        return;
                    }

                    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
                    try {
                        const intentRes = await fetch('?handler=CreateIntent', {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/x-www-form-urlencoded',
                                'RequestVerificationToken': token
                            },
                            body: new URLSearchParams({ amount: totalAmount.toString() })
                        });

                        const intentData = await intentRes.json();
                        if (intentData.error) {
                            showCardError(intentData.error);
                            if (btn) {
                                btn.disabled = false;
                                btn.innerHTML = '<i class="bi bi-save me-1"></i> Save Invoice';
                            }
                            return;
                        }

                        const { error, paymentIntent } = await stripe.confirmCardPayment(intentData.clientSecret, {
                            payment_method: { card: cardElement }
                        });

                        if (error) {
                            showCardError(error.message);
                            if (btn) {
                                btn.disabled = false;
                                btn.innerHTML = '<i class="bi bi-save me-1"></i> Save Invoice';
                            }
                            return;
                        }

                        intentInput.value = paymentIntent.id;
                        invoiceForm.submit();

                    } catch (err) {
                        showCardError('An unexpected error occurred during card processing. Please check card details.');
                        if (btn) {
                            btn.disabled = false;
                            btn.innerHTML = '<i class="bi bi-save me-1"></i> Save Invoice';
                        }
                    }
                }
            });
        }

        // Export public methods for HTML inline events
        window.invoiceApp = {
            addRow: addRow,
            removeRow: removeRow,
            updateRowTotal: updateRowTotal,
            onProductSelectChange: onProductSelectChange,
            onQtyInput: onQtyInput,
            togglePaymentDetails: togglePaymentDetails
        };

        // Add first row on load
        addRow();
    };
})(window);
