﻿<%@ Control Language="C#" AutoEventWireup="true" CodeFile="FunctionCallsDesigner.ascx.cs" Inherits="CompositeFunctionCallsDesigner.FunctionCallsDesigner" %>
<ui:functioneditor stateprovider="<%= this.SessionStateProvider %>" handle="<%= this.SessionStateId %>" id="<%=ClientID %>"/>