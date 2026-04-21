using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace KiteTodo.Services;

public class MarkdownPreviewService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string RenderFile(string filePath, int zoomPercent = 118, bool showOutline = true)
    {
        var markdown = File.ReadAllText(filePath, Encoding.UTF8);
        return RenderHtml(markdown, filePath, zoomPercent, showOutline);
    }

    public string RenderHtml(string markdown, string? filePath = null, int zoomPercent = 118, bool showOutline = true)
    {
        var body = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        var title = string.IsNullOrWhiteSpace(filePath)
            ? "Markdown 阅读"
            : Path.GetFileName(filePath);
        var safeZoom = Math.Clamp(zoomPercent, 85, 200);
        var zoomValue = (safeZoom / 100d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var showOutlineScriptValue = showOutline ? "true" : "false";

        var baseHref = string.Empty;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                baseHref = $"<base href=\"{new Uri(directory + Path.DirectorySeparatorChar).AbsoluteUri}\" />";
        }

        return $$"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta http-equiv="X-UA-Compatible" content="IE=edge" />
    <title>{{WebUtility.HtmlEncode(title)}}</title>
    {{baseHref}}
    <style>
        html {
            background: #f6f7fb;
            scroll-behavior: smooth;
        }

        body {
            margin: 0;
            padding: 8px 10px 24px;
            background: #f6f7fb;
            color: #1f2937;
            font-family: "Segoe UI", "Microsoft YaHei UI", sans-serif;
            line-height: 1.7;
            font-size: 16px;
            zoom: {{zoomValue}};
        }

        .page {
            box-sizing: border-box;
            width: 100%;
            max-width: none;
            margin: 0;
            position: relative;
            background: #ffffff;
            border: 1px solid #e5e7eb;
            border-radius: 16px;
            box-shadow: 0 14px 40px rgba(15, 23, 42, 0.08);
            padding: 24px 28px 30px;
        }

        .reader-shell {
            display: block;
        }

        .page.with-toc .reader-shell {
            display: grid;
            grid-template-columns: minmax(0, 1fr) 176px;
            column-gap: 12px;
            align-items: start;
        }

        .page.without-toc .reader-shell {
            display: block;
        }

        .article {
            min-width: 0;
            width: 100%;
            box-sizing: border-box;
        }

        .toc {
            width: 176px;
            position: sticky;
            top: 0;
            padding: 12px 10px 10px;
            border: 1px solid #e5e7eb;
            border-radius: 12px;
            background: #f8fafc;
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.5);
            overflow-y: auto;
            box-sizing: border-box;
            max-height: calc(100vh - 64px);
            align-self: start;
        }

        .toc.is-hidden {
            display: none;
        }

        .toc-title {
            margin: 0 0 10px;
            font-size: 12px;
            font-weight: 700;
            color: #0f172a;
        }

        .toc-list {
            margin: 0;
            padding: 0;
            list-style: none;
        }

        .toc-item {
            margin: 0;
            padding: 0;
        }

        .toc-link {
            position: relative;
            display: block;
            padding: 4px 6px 4px 10px;
            border-radius: 8px;
            color: #334155;
            text-decoration: none;
            white-space: normal;
            word-break: break-word;
            line-height: 1.45;
            font-size: 11px;
        }

        .toc-link::before {
            content: "";
            position: absolute;
            left: 0;
            top: 4px;
            bottom: 4px;
            width: 3px;
            border-radius: 999px;
            background: transparent;
            transition: background-color 0.18s ease;
        }

        .toc-link:hover {
            background: #e8f0fe;
            text-decoration: none;
        }

        .toc-link.is-active {
            background: #dbeafe;
            color: #0b57d0;
            font-weight: 600;
        }

        .toc-link.is-active::before {
            background: #0b57d0;
        }

        .toc-level-2 { padding-left: 14px; }
        .toc-level-3 { padding-left: 22px; }
        .toc-level-4 { padding-left: 30px; }

        h1, h2, h3, h4, h5, h6 {
            margin-top: 1.5em;
            margin-bottom: 0.6em;
            color: #0f172a;
            line-height: 1.3;
        }

        h1 { font-size: 34px; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.35em; }
        h2 { font-size: 27px; border-bottom: 1px solid #eef2f7; padding-bottom: 0.25em; }
        h3 { font-size: 22px; }

        p, ul, ol, blockquote, pre, .table-wrap {
            margin-top: 0.8em;
            margin-bottom: 0.8em;
        }

        a { color: #1664d9; text-decoration: none; }
        a:hover { text-decoration: underline; }

        code {
            font-family: "Cascadia Code", "Consolas", monospace;
            background: #f3f4f6;
            padding: 2px 6px;
            border-radius: 6px;
            font-size: 0.92em;
        }

        pre {
            background: #0f172a;
            color: #e2e8f0;
            padding: 14px 16px;
            border-radius: 12px;
            overflow-x: auto;
        }

        pre code {
            background: transparent;
            padding: 0;
            color: inherit;
        }

        blockquote {
            margin-left: 0;
            padding: 10px 16px;
            border-left: 4px solid #60a5fa;
            background: #eff6ff;
            color: #1e3a8a;
            border-radius: 8px;
        }

        .table-wrap {
            width: 100%;
            overflow-x: auto;
            overflow-y: hidden;
            border: 1px solid #e5e7eb;
            border-radius: 10px;
            background: #ffffff;
            box-sizing: border-box;
        }

        table {
            border-collapse: collapse;
            width: 100%;
            max-width: 100%;
            table-layout: auto;
        }

        th, td {
            border: 1px solid #e5e7eb;
            padding: 10px 12px;
            text-align: left;
            vertical-align: top;
            min-width: 0;
            white-space: normal;
            overflow-wrap: anywhere;
            word-break: break-word;
        }

        th {
            background: #f8fafc;
            font-weight: 600;
        }

        img {
            max-width: 100%;
            border-radius: 10px;
            display: block;
            margin: 12px 0;
        }

        hr {
            border: 0;
            border-top: 1px solid #e5e7eb;
            margin: 24px 0;
        }

        li { margin: 4px 0; }

        input[type="checkbox"] {
            width: 16px;
            height: 16px;
            margin-right: 8px;
            vertical-align: middle;
            accent-color: #1664d9;
            cursor: pointer;
        }

        .task-list-item {
            list-style: none;
            margin-left: -20px;
        }

        .task-list-item p {
            display: inline;
            margin: 0;
        }
    </style>
    <script>
        document.addEventListener('DOMContentLoaded', function () {
            var checkboxes = document.querySelectorAll('input[type="checkbox"]');
            for (var i = 0; i < checkboxes.length; i++) {
                checkboxes[i].removeAttribute('disabled');
            }

            var tables = document.querySelectorAll('table');
            for (var tableIndex = 0; tableIndex < tables.length; tableIndex++) {
                var table = tables[tableIndex];
                if (table.parentElement && table.parentElement.classList && table.parentElement.classList.contains('table-wrap')) {
                    continue;
                }

                var wrapper = document.createElement('div');
                wrapper.className = 'table-wrap';
                var parent = table.parentNode;
                if (!parent) {
                    continue;
                }

                parent.insertBefore(wrapper, table);
                wrapper.appendChild(table);
            }

            document.addEventListener('wheel', function (event) {
                if (!event.ctrlKey) {
                    return;
                }

                event.preventDefault();
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({
                        type: 'zoom-wheel',
                        delta: event.deltaY < 0 ? 1 : -1
                    });
                }
            }, { passive: false });

            var page = document.getElementById('page');
            var article = document.getElementById('article');
            var toc = document.getElementById('toc');
            var tocList = document.getElementById('toc-list');
            var shouldShowOutline = {{showOutlineScriptValue}};
            if (!page || !article || !toc || !tocList) {
                return;
            }

            var headings = article.querySelectorAll('h1, h2, h3, h4');
            if (!shouldShowOutline || !headings || headings.length < 3) {
                toc.className += ' is-hidden';
                page.className += ' without-toc';
                return;
            }

            page.className += ' with-toc';

            var links = [];
            function addClass(element, className) {
                if ((' ' + element.className + ' ').indexOf(' ' + className + ' ') < 0) {
                    element.className = (element.className ? element.className + ' ' : '') + className;
                }
            }

            function removeClass(element, className) {
                element.className = (' ' + element.className + ' ')
                    .replace(' ' + className + ' ', ' ')
                    .replace(/^\s+|\s+$/g, '');
            }

            function getScrollTop() {
                return window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
            }

            function setScrollTop(value) {
                document.documentElement.scrollTop = value;
                document.body.scrollTop = value;
            }

            function getElementTop(element) {
                var top = 0;
                while (element) {
                    top += element.offsetTop || 0;
                    element = element.offsetParent;
                }
                return top;
            }

            function smoothScrollTo(targetY) {
                var startY = getScrollTop();
                var distance = targetY - startY;
                var duration = 220;
                var startTime = new Date().getTime();

                function step() {
                    var now = new Date().getTime();
                    var progress = Math.min((now - startTime) / duration, 1);
                    var eased = 1 - Math.pow(1 - progress, 3);
                    setScrollTop(startY + distance * eased);
                    if (progress < 1) {
                        window.setTimeout(step, 16);
                    }
                }

                step();
            }

            for (var j = 0; j < headings.length; j++) {
                var heading = headings[j];
                var headingId = heading.getAttribute('id');
                if (!headingId) {
                    headingId = 'toc-heading-' + j;
                    heading.setAttribute('id', headingId);
                }

                var item = document.createElement('li');
                item.className = 'toc-item';

                var link = document.createElement('a');
                link.className = 'toc-link toc-level-' + heading.tagName.substring(1);
                link.href = '#' + headingId;
                link.innerText = heading.innerText || heading.textContent || ('标题 ' + (j + 1));
                link.addEventListener('click', function (event) {
                    event.preventDefault();

                    var targetId = this.getAttribute('href');
                    if (!targetId) {
                        return;
                    }

                    var target = document.querySelector(targetId);
                    if (!target) {
                        return;
                    }

                    smoothScrollTo(Math.max(getElementTop(target) - 12, 0));
                    if (history && history.replaceState) {
                        history.replaceState(null, '', targetId);
                    }
                });

                item.appendChild(link);
                tocList.appendChild(item);
                links.push(link);
            }

            function updateActiveHeading() {
                var activeIndex = 0;
                for (var k = 0; k < headings.length; k++) {
                    var rect = headings[k].getBoundingClientRect();
                    if (rect.top <= 140) {
                        activeIndex = k;
                    } else {
                        break;
                    }
                }

                for (var m = 0; m < links.length; m++) {
                    if (m === activeIndex) {
                        addClass(links[m], 'is-active');
                        if (links[m].scrollIntoView) {
                            links[m].scrollIntoView({ block: 'nearest', behavior: 'smooth' });
                        }
                    } else {
                        removeClass(links[m], 'is-active');
                    }
                }
            }

            updateActiveHeading();
            window.addEventListener('scroll', updateActiveHeading);
        });
    </script>
</head>
<body>
    <div class="page" id="page">
        <div class="reader-shell">
            <div class="article" id="article">
                {{body}}
            </div>
            <aside class="toc" id="toc">
                <div class="toc-title">目录</div>
                <ul class="toc-list" id="toc-list"></ul>
            </aside>
        </div>
    </div>
</body>
</html>
""";
    }

    public int CountOutlineHeadings(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return 0;

        return Regex.Matches(markdown, @"^#{1,4}\s+\S+", RegexOptions.Multiline).Count;
    }
}