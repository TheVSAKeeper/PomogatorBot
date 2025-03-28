function updateFavoritesOrder() {
    const favoritesList = document.getElementById('favorites-list');
    const newOrder = [...favoritesList.children].map(item =>
        messageHistory[parseInt(item.dataset.index)]
    );

    const remaining = messageHistory.filter(msg => !msg.favorite);
    messageHistory = [...newOrder, ...remaining];
    localStorage.setItem('messageHistory', JSON.stringify(messageHistory));
}

function toggleFavorite(index) {
    if (index === editingIndex) {
        cancelEdit();
    }
    messageHistory[index].favorite = !messageHistory[index].favorite;
    localStorage.setItem('messageHistory', JSON.stringify(messageHistory));
    updateHistory();
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
