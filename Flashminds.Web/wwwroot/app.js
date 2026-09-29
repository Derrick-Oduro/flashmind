// Theme management
function setTheme(theme) {
    const html = document.documentElement;
    if (theme === 'dark') {
        html.setAttribute('data-bs-theme', 'dark');
        document.body.setAttribute('data-bs-theme', 'dark');
    } else {
        html.removeAttribute('data-bs-theme');
        document.body.removeAttribute('data-bs-theme');
    }
}

// Toggle theme
function toggleTheme() {
    const html = document.documentElement;
    const currentTheme = html.getAttribute('data-bs-theme') || 'dark';
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    setTheme(newTheme);
    localStorage.setItem('theme', newTheme);
}

// Initialize theme on page load
window.addEventListener('DOMContentLoaded', function () {
    const savedTheme = localStorage.getItem('theme') || 'dark';
    setTheme(savedTheme);
});
