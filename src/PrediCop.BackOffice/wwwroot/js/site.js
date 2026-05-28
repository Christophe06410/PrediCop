// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Tri de tableaux par colonne
function initSortableTables() {
    document.querySelectorAll('table[data-sortable]').forEach(table => {
        const headers = table.querySelectorAll('thead th[data-col]');
        let currentCol = null, ascending = true;

        headers.forEach(th => {
            th.style.cursor = 'pointer';
            th.style.userSelect = 'none';
            th.addEventListener('click', () => {
                const col = parseInt(th.dataset.col);
                if (currentCol === col) {
                    ascending = !ascending;
                } else {
                    currentCol = col;
                    ascending = true;
                }
                // Mettre à jour les flèches
                headers.forEach(h => {
                    const arrow = h.querySelector('.sort-arrow');
                    if (arrow) arrow.remove();
                });
                const arrow = document.createElement('span');
                arrow.className = 'sort-arrow ms-1';
                arrow.textContent = ascending ? '▲' : '▼';
                th.appendChild(arrow);

                // Trier les lignes
                const tbody = table.querySelector('tbody');
                const rows = Array.from(tbody.querySelectorAll('tr'));
                rows.sort((a, b) => {
                    const aCell = a.cells[col];
                    const bCell = b.cells[col];
                    if (!aCell || !bCell) return 0;
                    const aVal = (aCell.dataset.sort ?? aCell.textContent).trim();
                    const bVal = (bCell.dataset.sort ?? bCell.textContent).trim();
                    // Essai numérique, sinon alphabétique
                    const aNum = parseFloat(aVal.replace(/[^\d.-]/g, ''));
                    const bNum = parseFloat(bVal.replace(/[^\d.-]/g, ''));
                    let cmp = (!isNaN(aNum) && !isNaN(bNum))
                        ? aNum - bNum
                        : aVal.localeCompare(bVal, 'fr');
                    return ascending ? cmp : -cmp;
                });
                rows.forEach(r => tbody.appendChild(r));
            });
        });
    });
}
document.addEventListener('DOMContentLoaded', initSortableTables);
