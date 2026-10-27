/* CPU stub. ggml.dll imports ggml_backend_vk_reg from the Vulkan library.
   Returning null skips Vulkan. Rebuild from this directory:

   Windows:
     x86_64-w64-mingw32-gcc -shared -nostdlib -ffreestanding -fno-stack-protector -O2 -s \
       -o ../Plugins/Windows/ggml-vulkan.dll ggml-vulkan-stub.c \
       -Wl,-e,DllMain,--kill-at -lkernel32

   Linux:
     gcc -shared -fPIC -O2 -s -o ../Plugins/Linux/libggml-vulkan.so ggml-vulkan-stub.c
*/
#if defined(_WIN32)
__declspec(dllexport) void * ggml_backend_vk_reg(void)
{
    return 0;
}

int DllMain(void * instance, unsigned long reason, void * reserved)
{
    (void)instance;
    (void)reason;
    (void)reserved;
    return 1;
}
#else
void * ggml_backend_vk_reg(void)
{
    return 0;
}
#endif
