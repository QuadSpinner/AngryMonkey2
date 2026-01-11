# AngryMonkey v2

![AngryMonkey Logo](AngryMonkey.png)

AngryMonkey was born out of frustration with existing documentation 
building and hosting tools. With a lot of AI-bile in them, these 
tools also carry and overinflated price tag. Some of them don't 
even take off their branding when you pay $300 a month!

And even after all that expense, they still fail to deliver a
proper experience both for the end-user and the developer.

AngryMonkey was written over Christmas 2025 break to scratch
this itch. It's free, open-source, and designed to be simple.

It is meant to be a starting point for developers who want to
build their own system. What we have now is what we needed for
Gaea's Documentation. You can fork it, modify it, and make it
your own.

[See it in action!](https://docs.gaea.app)

---

## Features
- **Static Site Generation**: Generates a static website for fast loading and easy hosting.
- **Markdown-Based**: Write your documentation in simple Markdown files.
- **Simple Configuration**: Configure your documentation site with a straightforward JSON file and YAML headers.
- **Hierarchical Structure**: Organize your documentation in a tree-like structure with nested sections.
  - **Support multiple Hives/Sections**: Organize your documentation into multiple hives or sections for better structure.
  - **Easy File Management**: Organize your documentation files in a straightforward directory structure.
  - **Slug based linking**: Easily create links between pages using slugs. Prevents links from breaking even when you move pages.
- **Fast Build Times**: Optimized for quick builds even with large documentation sets.
- **LUNR.js Search**: Integrated client-side search functionality using LUNR.js.
- **Customizable Themes**: Easily modify the look and feel of your documentation.
- **Open Source**: Completely free and open-source under the MIT License.

--

## Basic Setup

1. **Clone the Repository**
```
   git clone https://github.com/QuadSpinner/AngryMonkey2.git
   cd AngryMonkey2
```

2. **Install Dependencies**
- Ensure you have [.NET 10 SDK](https://dotnet.microsoft.com/) installed.

3. **Configure Your Documentation**

AngryMonkey uses a `hives.json` file in the project root to define your documentation structure and build settings.  
Below is an example configuration and explanation of each field:

```
   {
     "RootFolder": "X:\\Docs\\AngryMonkey",
     "SourceRoot": "Source",
     "StagingRoot": "staging",
     "TemplatesFolder": "template",
     "HtmlTemplate": "template\\template.html",
     "DataFolders": [".data"],
     "Hives": [
       {
         "Name": "Main Docs",
         "Folder": "",
         "ShortName": "main",
         "IsHome": true,
         "Url": "/"
       },
       {
         "Name": "API Reference",
         "Folder": "api",
         "ShortName": "api",
         "IsHome": false,
         "Url": "/api"
       }
     ]
   }
```

**Configuration Fields:**

- `RootFolder`: The absolute path to your AngryMonkey project root.
- `SourceRoot`: Path (relative to `RootFolder`) where your Markdown source files are located.
- `StagingRoot`: Path (relative to `RootFolder`) where the generated static site will be output.
- `TemplatesFolder`: Path to your HTML templates.
- `HtmlTemplate`: Path to the main HTML template file.
- `DataFolders`: Array of additional data folders to copy into the output (e.g., for images or attachments).
- `Hives`: List of documentation sections ("hives"). Each hive has:
  - `Name`: Display name for the section.
  - `Folder`: Subfolder under `SourceRoot` for this hive's content.
  - `ShortName`: Short identifier for the hive.
  - `IsHome`: Set to `true` for the main/home documentation section.
  - `Url`: The base URL path for this hive in the generated site.

4. **Build the Documentation**
```
   dotnet build
```

5. **Generate the Site**
```
   dotnet run
```
The generated static site will be output to your configured staging directory.

6. **Preview or Deploy**
   - Open the generated HTML files in your browser, or deploy the output directory to your preferred static hosting provider.