var WebGLFileUploaderPlugin = {
    TriggerWebGLFilePicker: function (objectNamePtr, callbackMethodPtr) {
        var objectName = UTF8ToString(objectNamePtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);

        var oldInput = document.getElementById('UnityWebGLFileInput');
        if (oldInput) {
            oldInput.remove();
        }

        var fileInput = document.createElement('input');
        fileInput.id = 'UnityWebGLFileInput';
        fileInput.type = 'file';
        fileInput.accept = 'image/png, image/jpeg, image/jpg';
        fileInput.style.display = 'none';
        document.body.appendChild(fileInput);

        fileInput.onchange = function (event) {
            var file = event.target.files[0];
            if (!file) return;

            var reader = new FileReader();
            reader.onload = function (e) {
                var base64Data = e.target.result.split(',')[1];
                SendMessage(objectName, callbackMethod, base64Data);
                fileInput.remove();
            };
            reader.readAsDataURL(file);
        };

        fileInput.click();
    }
};

mergeInto(LibraryManager.library, WebGLFileUploaderPlugin);