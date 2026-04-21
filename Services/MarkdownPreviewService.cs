using System.IO;
using System.Net;
using System.Text;
using Markdig;

namespace KiteTodo.Services;

public class MarkdownPreviewService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string RenderFile(string filePath)
    {
        var markdown = File.ReadAllText(filePath, Encoding.UTF8);
        return RenderHtml(markdown, filePath);
    }

    public string RenderHtml(string markdown, string? filePath = null)
    {
        var body = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        var title = string.IsNullOrWhiteSpace(filePath)
            ? "Markdown 阅读"
            : Path.GetFileName(filePath);

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
        body {
            margin: 0;
            padding: 28px 32px 80px;
            background: #f6f7fb;
            color: #1f2937;
            font-family: "Segoe UI", "Microsoft YaHei UI", sans-serif;
            line-height: 1.7;
            font-size: 15px;
        }

        .page {
            max-width: 920px;
            margin: 0 auto;
            background: #ffffff;
            border: 1px solid #e5e7eb;
            border-radius: 16px;
            box-shadow: 0 14px 40px rgba(15, 23, 42, 0.08);
            padding: 28px 34px;
        }

        h1, h2, h3, h4, h5, h6 {
            margin-top: 1.5em;
            margin-bottom: 0.6em;
            color: #0f172a;
            line-height: 1.3;
        }

        h1 { font-size: 30px; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.35em; }
        h2 { font-size: 24px; border-bottom: 1px solid #eef2f7; padding-bottom: 0.25em; }
        h3 { font-size: 20px; }

        p, ul, ol, blockquote, pre, table {
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

        table {
            border-collapse: collapse;
            width: 100%;
            overflow: hidden;
            border-radius: 10px;
        }

        th, td {
            border: 1px solid #e5e7eb;
            padding: 10px 12px;
            text-align: left;
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
        });
    </script>
</head>
<body>
    <div class="page">
        {{body}}
    </div>
</body>
</html>
""";
    }
}