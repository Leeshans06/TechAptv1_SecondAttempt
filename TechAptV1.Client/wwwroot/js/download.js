window.downloadFileStream = async (fileName, contentType, streamRef) => {
    try {
        // Try to obtain the file stream
        const fileStream = await streamRef.stream();
        const data = await new Response(fileStream).blob();

        const url = window.URL.createObjectURL(data);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
    }
    catch (error) {
        console.error("Error in js download.js file :", error);
        // Inform the user about the error
        alert("An error occurred. Please try again.");
    }
};
