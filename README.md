# dropmaker
Batch image resizing &amp; watermarking software

## Q&amp;A

#### Q: How this software could help you?

A: Imagine you have hundreds, thousands of pictures you need to resize and apply watermark to it before putting it to a website. This tool is made for solving exactly the same problem I had. I tried lot's of exists tools but did not found anything that solves all my issues. So, I build my own solution.

#### Q: How to use the software?

A: Check out next sections. I write all details from installing to running this software.

#### Q: What about preformance?

A: I tried my best to make things faster. You can run this software multithreaded which will spread processing tasks over CPU cores.

## Installation

#### 1. Install dotnet runtime

Because I build this software using C# for .NET Core. So you need runtime to run this software.

<table>
<tr>
  <th>Windows</th>
  <th>Linux</th>
  <th>Mac OS</th>
</tr>
<tr>
  <td>
    
Check [download page](https://dotnet.microsoft.com/download/dotnet-core/current/runtime) to download dotnet runtime installer to your windows machine. Click next, next, next then finish.

  </td>
  <td>
  
Check [this page](https://docs.microsoft.com/en-us/dotnet/core/install/linux) to learn how to install dotnet runtime to your linux distrubition.

  </td>
  <td>
Idk, I'm not rich. Yet.
  </td>
</th>
</table>

#### 2. Download latest build

You can download latest build from [GitHub Actions Artifacts](https://github.com/TheMisir/dropmaker/actions) or you can build yourself if you have dotnet sdk installed. I suggest downloading from GH Actions Artifacts. Then unzip the zip file and you're ready.

### 3. (Optional) Give execute permission to `dropmaker.sh`

Skip if you are using windows machine. Open your terminal and type:

```bash
chmod +x dropmaker.sh
```

## Instructions

You can run software by using terminal (cmd or powershell in Windows).

```sh
dropmaker.sh --help
```

