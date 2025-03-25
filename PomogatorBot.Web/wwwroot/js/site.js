let messageHistory = JSON.parse(localStorage.getItem('messageHistory')) || [];
let editingIndex = -1;

messageHistory = messageHistory.map(msg => ({
    ...msg,
    favorite: msg.favorite || false
}));

async function sendMessage() {
    const button = document.querySelector('.send-button');
    const textarea = document.querySelector('.message-field');

    const subscribes = Array.from(document.querySelectorAll('input[name="subscribes"]:checked'))
        .reduce((acc, checkbox) => acc | parseInt(checkbox.value), 0);

    if (editingIndex !== -1) {
        saveEdit(false);
    }

    textarea.classList.remove('error');

    if (!textarea.value.trim()) {
        textarea.classList.add('error');
        return;
    }

    button.classList.add('loading');
    button.innerHTML = '⏳ Отправляем...';

    try {
        const response = await fetch('/notify', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                message: textarea.value,
                subscribes
            })
        });

        if (response.ok) {
            const newMessage = {
                text: textarea.value,
                date: new Date().toLocaleString(),
                status: 'success'
            };
            addToHistory(newMessage);
            textarea.classList.remove('error');
            textarea.value = '';
            showNotification('✅ Послание доставлено!', '#00bcd4');
        } else {
            throw new Error('Ошибка отправки');
        }
    } catch (error) {
        const errorMessage = {
            text: textarea.value,
            date: new Date().toLocaleString(),
            status: 'error'
        };
        addToHistory(errorMessage);
        console.error('Error:', error);
        showNotification('❌ Ошибка отправки', '#ff6699');
    } finally {
        button.classList.remove('loading');
        button.innerHTML = 'Возвестить пастве';
    }
}

function showNotification(text, color) {
    const notification = document.getElementById('notification');
    notification.textContent = text;
    notification.style.backgroundColor = color;
    notification.style.display = 'block';

    setTimeout(() => {
        notification.style.display = 'none';
    }, 3000);
}

document.querySelector('.message-field').addEventListener('input', function () {
    this.classList.remove('error');
    this.style.borderColor = '';
});

function addToHistory(message) {
    messageHistory.unshift(message);
    if (messageHistory.length > 50) messageHistory.pop();
    localStorage.setItem('messageHistory', JSON.stringify(messageHistory));
    updateHistory();
}

function updateHistory() {
    const historyList = document.getElementById('history-list');
    const favoritesList = document.getElementById('favorites-list');
    historyList.innerHTML = '';
    favoritesList.innerHTML = '';

    const favorites = messageHistory.map((msg, index) => ({ msg, index })).filter(msg => msg.msg.favorite);
    const regular = messageHistory.map((msg, index) => ({ msg, index })).filter(msg => !msg.msg.favorite);

    if (favorites.length > 0) {
        favorites.forEach(msg => {
            const item = createMessageElement(msg.msg, msg.index, true);
            favoritesList.appendChild(item);
        });
    } else {
        favoritesList.innerHTML = '<div class="empty-history">Нет избранных сообщений</div>';

    }

    if (regular.length > 0) {
        regular.forEach(msg => {
            const item = createMessageElement(msg.msg, msg.index, false);
            historyList.appendChild(item);
        });
    } else {
        historyList.innerHTML = '<div class="empty-history">Нет обычных сообщений</div>';
    }


    initDragAndDrop();
}

function createMessageElement(msg, index, isFavorite) {
    const item = document.createElement('div');
    item.className = `message-item ${msg.status === 'success' ? 'message-success' : 'message-error'}
                         ${isFavorite ? 'draggable message-favorite' : ''}`;
    item.dataset.index = index;
    item.draggable = isFavorite;

    item.addEventListener('contextmenu', e => {
        if (isFavorite) {
            e.preventDefault();
            startEditMessage(index, msg.text);
            return false;
        }
    });

    const safeDate = escapeHtml(msg.date);
    const safeText = escapeHtml(msg.text);

    item.innerHTML = `
            <div class="message-date">${safeDate}</div>
            <div class="message-preview">${safeText}</div>
            <div class="message-actions">
                <div class="copy-notice">Скопировано!</div>
                <button class="action-btn favorite-btn ${msg.favorite ? 'active' : ''}"
                        data-index="${index}">
                    ${msg.favorite ? '⭐' : '✩'}
                </button>
                <div class="delete-btn">
                    <button class="action-btn delete-action" data-index="${index}">🗑️</button>
                </div>
                <div class="delete-confirm">
                    <button class="action-btn confirm-delete-btn" data-index="${index}">✅</button>
                    <button class="action-btn cancel-delete-btn">❌</button>
                </div>
            </div>
        `;

    const favoriteBtn = item.querySelector('.favorite-btn');
    favoriteBtn.addEventListener('click', e => {
        e.stopPropagation();
        toggleFavorite(index);
    });

    const deleteBtn = item.querySelector('.delete-action');
    const confirmBtn = item.querySelector('.confirm-delete-btn');
    const cancelBtn = item.querySelector('.cancel-delete-btn');

    deleteBtn.addEventListener('click', () => item.classList.add('confirm-delete'));
    cancelBtn.addEventListener('click', () => item.classList.remove('confirm-delete'));
    confirmBtn.addEventListener('click', () => deleteMessage(index));

    item.addEventListener('click', e => {
        if (!e.target.closest('.message-actions')) {
            const textarea = document.querySelector('.message-field');
            textarea.value = msg.text;
            textarea.focus();
            item.classList.add('copied');
            setTimeout(() => item.classList.remove('copied'), 1000);
        }
    });

    return item;
}

function startEditMessage(index, text) {
    if (editingIndex === index) {
        cancelEdit();
        return;
    }

    editingIndex = index;
    document.querySelector('.message-field').value = text;
    document.querySelector('.button-group').classList.add('editing-mode');

    document.querySelectorAll('.message-item').forEach(item => item.classList.remove('editing'));
    document.querySelector(`.message-item[data-index="${index}"]`).classList.add('editing');
}

function cancelEdit() {
    editingIndex = -1;
    document.querySelector('.message-field').value = '';
    document.querySelector('.button-group').classList.remove('editing-mode');
    document.querySelectorAll('.message-item').forEach(item => item.classList.remove('editing'));
}

function saveEdit(isClear = true) {
    if (editingIndex === -1) return;

    const newText = document.querySelector('.message-field').value.trim();
    if (!newText) {
        showNotification('❌ Текст не может быть пустым', '#ff6699');
        return;
    }

    messageHistory[editingIndex].text = newText;
    localStorage.setItem('messageHistory', JSON.stringify(messageHistory));

    document.querySelector(`.message-item[data-index="${editingIndex}"]`).classList.remove('editing');
    updateHistory();
    showNotification('✅ Изменения сохранены', '#ffcc00');
    editingIndex = -1;
    document.querySelector('.button-group').classList.remove('editing-mode');
    if (isClear) {
        document.querySelector('.message-field').value = '';
    }
}

function initDragAndDrop() {
    const draggables = document.querySelectorAll('.draggable');
    const container = document.getElementById('favorites-list');

    draggables.forEach(draggable => {
        draggable.addEventListener('dragstart', () => draggable.classList.add('dragging'));

        draggable.addEventListener('dragend', () => draggable.classList.remove('dragging'));
    });

    container.addEventListener('dragover', e => {
        e.preventDefault();
        const afterElement = getDragAfterElement(container, e.clientY);
        const draggable = document.querySelector('.dragging');
        if (afterElement == null) {
            container.appendChild(draggable);
        } else {
            container.insertBefore(draggable, afterElement);
        }
        updateFavoritesOrder();
    });
}

function getDragAfterElement(container, y) {
    const draggableElements = [...container.querySelectorAll('.draggable:not(.dragging)')];

    return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;
        if (offset < 0 && offset > closest.offset) {
            return { offset, element: child };
        } else {
            return closest;
        }
    }, { offset: Number.NEGATIVE_INFINITY }).element;
}

function updateFavoritesOrder() {
    const favoritesList = document.getElementById('favorites-list');
    const newOrder = [...favoritesList.children].map(item =>
        messageHistory[parseInt(item.dataset.index)]
    );

    const remaining = messageHistory.filter(msg => !msg.favorite);
    messageHistory = [...newOrder, ...remaining];
    localStorage.setItem('messageHistory', JSON.stringify(messageHistory));
}

function deleteMessage(index) {
    messageHistory.splice(index, 1);
    localStorage.setItem('messageHistory', JSON.stringify(messageHistory));
    updateHistory();
    showNotification('🗑️ Сообщение удалено', '#666');
}

function toggleFavorite(index) {
    if (index === editingIndex) {
        cancelEdit();
    }
    messageHistory[index].favorite = !messageHistory[index].favorite;
    localStorage.setItem('messageHistory', JSON.stringify(messageHistory));
    updateHistory();
}

function clearHistory() {
    if (confirm('Очистить всю историю, кроме избранных сообщений?')) {
        messageHistory = messageHistory.filter(msg => msg.favorite);
        localStorage.setItem('messageHistory', JSON.stringify(messageHistory));
        updateHistory();
        showNotification('История очищена, избранные сохранены', '#00bcd4');
    }
}

function escapeHtml(unsafe) {
    return unsafe.replace(/[&<>"']/g, function (match) {
        return {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        }[match];
    });
}

updateHistory();
