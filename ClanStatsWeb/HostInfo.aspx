<%@ Page Language="C#" %>

<%@ Import Namespace="System.Globalization" %>
<%@ Import Namespace="Microsoft.Win32" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Diagnostico Não Intrusivo do Ambiente</title>

    <style type="text/css">
        table {
            border: 1px gray solid;
            border-collapse: collapse;
        }

        th, td {
            border: 1px gray solid;
        }
    </style>


</head>
<body>
    <h1>Diagnostico Não Intrusivo do Ambiente</h1>
    <table>
        <thead>
            <tr>
                <th>Elemento</th>
                <th>Valor</th>
            </tr>
        </thead>
        <tfoot />
        <tbody>
            <tr>
                <td>Versão do .Net Framework</td>
                <td><% Response.Write(Environment.Version.ToString()); %></td>
            </tr>
            <tr>
                <td>Versão do .Net Framework (via Registro)</td>
                <td>
                    <% 
                        // Determina a versão do Framework via registro
                        // Codigo Adaptado de https://msdn.microsoft.com/en-us/library/hh925568(v=vs.110).aspx
                        string version;
                        try
                        {
                            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
                            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
                            {
                                if (ndpKey == null)
                                {
                                    version = "< 4.5";
                                }
                                else
                                {
                                    if (ndpKey.GetValue("Release") == null)
                                    {
                                        version = "< 4.5";
                                    }
                                    else
                                    {
                                        int releaseKey = (int) ndpKey.GetValue("Release");
                                        if (releaseKey >= 461808)
                                        {
                                            version = "4.7.2 or latter";
                                        }
                                        else if (releaseKey >= 461308)
                                        {
                                            version = "4.7.1";
                                        }
                                        else if (releaseKey >= 460798)
                                        {
                                            version = "4.7";
                                        }
                                        else if (releaseKey >= 394802)
                                        {
                                            version = "4.6.2";
                                        }
                                        else if (releaseKey >= 394254)
                                        {
                                            version = "4.6.1";
                                        }
                                        else if (releaseKey >= 393295)
                                        {
                                            version = "4.6";
                                        }
                                        else if ((releaseKey >= 379893))
                                        {
                                            version = "4.5.2";
                                        }
                                        else if ((releaseKey >= 378675))
                                        {
                                            version = "4.5.1";
                                        }
                                        else if ((releaseKey >= 378389))
                                        {
                                            version = "4.5";
                                        }
                                        else
                                        {
                                            version = "< 4.5";
                                        }

                                        version = version + " (" + releaseKey + ")";
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            version = ex.Message;
                        }
                        Response.Write(version);
                    %>
                </td>
            </tr>
            <tr>
                <td>Versão do Sistema Operacional</td>
                <td><% Response.Write(Environment.OSVersion.ToString()); %></td>
            </tr>
            <tr>
                <td>SO 64bits</td>
                <td><% Response.Write(Environment.Is64BitOperatingSystem.ToString()); %></td>
            </tr>
            <tr>
                <td>Processo 64bits</td>
                <td><% Response.Write(Environment.Is64BitProcess.ToString()); %></td>
            </tr>
            <tr>
                <td>Número de Processadores</td>
                <td><% Response.Write(Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)); %></td>
            </tr>
            <tr>
                <td>Nome da Maquina</td>
                <td><% Response.Write(Environment.MachineName); %></td>
            </tr>
            <tr>
                <td>Path</td>
                <td><% Response.Write(HttpContext.Current.Request.Path); %></td>
            </tr>
            <tr>
                <td>Url</td>
                <td><% Response.Write(HttpContext.Current.Request.Url); %></td>
            </tr>
            <tr>
                <td>PhysicalPath</td>
                <td><% Response.Write(HttpContext.Current.Request.PhysicalPath); %></td>
            </tr>
            <tr>
                <td>HTTP_COOKIE</td>
                <td><% Response.Write(HttpContext.Current.Request.ServerVariables["HTTP_COOKIE"]); %></td>
            </tr>
            <tr>
                <td>HTTP_HOST</td>
                <td><% Response.Write(HttpContext.Current.Request.ServerVariables["HTTP_HOST"]); %></td>
            </tr>
            <tr>
                <td>SERVER_NAME</td>
                <td><% Response.Write(HttpContext.Current.Request.ServerVariables["SERVER_NAME"]); %></td>
            </tr>
            <tr>
                <td>SERVER_PORT</td>
                <td><% Response.Write(HttpContext.Current.Request.ServerVariables["SERVER_PORT"]); %></td>
            </tr>
            <tr>
                <td>Local Time</td>
                <td><% Response.Write(DateTime.Now.ToString("o")); %></td>
            </tr>
            <tr>
                <td>UTC Time</td>
                <td><% Response.Write(DateTime.UtcNow.ToString("o")); %></td>
            </tr>
            <tr style='background-color: lightgreen;'>
                <td>Raw Query String</td>
                <td><% Response.Write(HttpContext.Current.Request.QueryString); %></td>
            </tr>
            <%
                foreach (var key in HttpContext.Current.Request.QueryString.AllKeys)
                {
                    string value = HttpContext.Current.Request.QueryString[key];
                    Response.Write("<tr style='background-color: lightgreen;'><td>QS Key: '" + key + "'</td><td>" + value + "</td></tr>");
                }
            %>
            <%
                foreach (var serverVariable in HttpContext.Current.Request.ServerVariables)
                {
                    if (serverVariable.ToString().Contains("ALL_HTTP") || serverVariable.ToString().Contains("ALL_RAW"))
                    {
                        continue;
                    }
                    string name = serverVariable.ToString();
                    string value = HttpContext.Current.Request.ServerVariables[name];
                    Response.Write("<tr style='background-color: lightgoldenrodyellow;'><td>" + name + "</td><td>" + value + "</td></tr>");
                }
            %>
        </tbody>
    </table>
</body>
</html>
