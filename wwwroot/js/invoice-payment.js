(function (window) {
    'use strict';

    const cardContainer = document.getElementById('modalStripeCardContainer');
    const stripeKey = cardContainer?.dataset.stripeKey || '';
    let stripe = null;
    let elements = null;
    let modalCardElement = null;
    let isModalCardMounted = false;

    function showCardError(message) {
        const alertBox = document.getElementById('modal-card-alert');
        const errorSpan = document.getElementById('modal-card-error-msg');
        if (alertBox && errorSpan) {
            errorSpan.textContent = message;
            alertBox.style.display = 'flex';
        }
    }

    function clearCardError() {
        const alertBox = document.getElementById('modal-card-alert');
        const errorSpan = document.getElementById('modal-card-error-msg');
        if (alertBox && errorSpan) {
            errorSpan.textContent = '';
            alertBox.style.display = 'none';
        }
    }

    function mountCardElement() {
        if (!stripeKey || typeof Stripe === 'undefined' || !cardContainer ||
            cardContainer.style.display === 'none') return;

        if (!stripe) {
            stripe = Stripe(stripeKey);
            elements = stripe.elements();
            modalCardElement = elements.create('card', {
                style: {
                    base: {
                        fontSize: '16px',
                        color: '#0F172A',
                        fontFamily: 'system-ui, -apple-system, sans-serif',
                        '::placeholder': { color: '#64748B' }
                    },
                    invalid: { color: '#DC2626' }
                }
            });
            modalCardElement.on('change', event => {
                if (event.error) showCardError(event.error.message);
                else clearCardError();
            });
        }

        if (!isModalCardMounted) {
            modalCardElement.mount('#modal-card-element');
            isModalCardMounted = true;
        }
    }

    window.toggleModalStripe = function (method) {
        if (!cardContainer) return;

        const showCard = method === 'Card';
        cardContainer.style.display = showCard ? 'block' : 'none';
        if (showCard) {
            window.setTimeout(mountCardElement, 100);
        } else {
            clearCardError();
        }
    };

    const payForm = document.getElementById('payRemainingForm');
    if (!payForm) return;

    payForm.addEventListener('submit', async function (event) {
        const method = document.getElementById('modalPaymentMethod').value;
        const intentInput = document.getElementById('modalStripePaymentIntentId');

        if (method !== 'Card' || !intentInput || intentInput.value) return;

        event.preventDefault();
        clearCardError();

        const button = document.getElementById('confirmPayBtn');
        if (button) {
            button.disabled = true;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing Card…';
        }

        const amountInput = document.getElementById('modalPayAmount');
        const amount = parseFloat(amountInput ? amountInput.value : '0');
        if (!amount || amount <= 0) {
            showCardError('Please enter a valid payment amount.');
            if (button) {
                button.disabled = false;
                button.innerHTML = '<i class="bi bi-check-circle me-1"></i> Process Payment';
            }
            return;
        }

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
        try {
            const requestBody = new FormData(payForm);
            requestBody.set('amount', amount.toString());
            const intentResponse = await fetch('?handler=CreateIntent', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token },
                body: requestBody
            });

            const intentData = await intentResponse.json();
            if (intentData.error) {
                showCardError(intentData.error);
                if (button) {
                    button.disabled = false;
                    button.innerHTML = '<i class="bi bi-check-circle me-1"></i> Process Payment';
                }
                return;
            }

            const { error, paymentIntent } = await stripe.confirmCardPayment(intentData.clientSecret, {
                payment_method: { card: modalCardElement }
            });

            if (error) {
                showCardError(error.message);
                if (button) {
                    button.disabled = false;
                    button.innerHTML = '<i class="bi bi-check-circle me-1"></i> Process Payment';
                }
                return;
            }

            intentInput.value = paymentIntent.id;
            payForm.submit();
        } catch (error) {
            showCardError('An unexpected error occurred during card processing.');
            if (button) {
                button.disabled = false;
                button.innerHTML = '<i class="bi bi-check-circle me-1"></i> Process Payment';
            }
        }
    });
})(window);
