	.arch armv7-a
	.fpu vfpv3-d16
	.eabi_attribute 20, 1
	.eabi_attribute 21, 1
	.eabi_attribute 23, 3
	.eabi_attribute 24, 1
	.eabi_attribute 25, 1
	.eabi_attribute 26, 2
	.eabi_attribute 30, 2
	.eabi_attribute 34, 1
	.eabi_attribute 18, 4
	.file	"20261005-converter-ratio-compiler-probe.c"
	.text
	.align	1
	.p2align 2,,3
	.global	branch_ratio
	.syntax unified
	.thumb
	.thumb_func
	.type	branch_ratio, %function
branch_ratio:
	@ args = 0, pretend = 0, frame = 16
	@ frame_needed = 0, uses_anonymous_args = 0
	push	{r4, r5, r6, r7, r8, lr}
	mov	r8, r1
	ldr	r3, .L8
	vpush.64	{d8, d9}
	sub	sp, sp, #16
	add	r1, sp, #4
	mov	r4, r0
	ldr	r3, [r3]
	str	r3, [sp, #12]
	mov	r3, #0
	bl	approx
	mov	r2, r0
	mov	r3, r1
	mov	r0, r8
	add	r1, sp, #8
	vmov	d8, r2, r3
	bl	approx
	ldrd	r3, r2, [sp, #4]
	vmov	d9, r0, r1
	mov	r0, r4
	subs	r4, r3, r2
	bl	limbs
	mov	r5, r0
	mov	r0, r8
	bl	limbs
	subs	r5, r5, r0
	add	r0, r4, r5, lsl #5
	cmp	r0, #0
	ble	.L2
	vmov	r6, r7, d8	@ int
	vmov	r3, s17	@ int
	add	r7, r3, r0, lsl #20
	vmov	d8, r6, r7
.L3:
	vdiv.f64	d7, d8, d9
	ldr	r3, .L8
	ldr	r2, [r3]
	ldr	r3, [sp, #12]
	eors	r2, r3, r2
	mov	r3, #0
	bne	.L7
	vmov	r0, r1, d7
	add	sp, sp, #16
	@ sp needed
	vldm	sp!, {d8-d9}
	pop	{r4, r5, r6, r7, r8, pc}
.L2:
	vmov	r2, r3, d9	@ int
	vmov	r1, s19	@ int
	sub	r3, r1, r0, lsl #20
	vmov	d9, r2, r3
	b	.L3
.L7:
	bl	__stack_chk_fail
.L9:
	.align	2
.L8:
	.word	__stack_chk_guard
	.size	branch_ratio, .-branch_ratio
	.align	1
	.p2align 2,,3
	.global	selected_pointer_ratio
	.syntax unified
	.thumb
	.thumb_func
	.type	selected_pointer_ratio, %function
selected_pointer_ratio:
	@ args = 0, pretend = 0, frame = 32
	@ frame_needed = 0, uses_anonymous_args = 0
	push	{r4, r5, r6, r8, r9, lr}
	mov	r6, r1
	ldr	r3, .L16
	sub	sp, sp, #32
	mov	r4, r0
	mov	r1, sp
	ldr	r3, [r3]
	str	r3, [sp, #28]
	mov	r3, #0
	bl	approx
	mov	r2, r0
	mov	r3, r1
	mov	r0, r6
	add	r1, sp, #4
	strd	r2, [sp, #8]
	bl	approx
	ldr	r2, [sp, #4]
	ldr	r3, [sp]
	mov	r8, r0
	mov	r0, r4
	mov	r9, r1
	subs	r4, r3, r2
	strd	r8, [sp, #16]
	bl	limbs
	mov	r5, r0
	mov	r0, r6
	bl	limbs
	subs	r5, r5, r0
	add	r0, r4, r5, lsl #5
	cmp	r0, #0
	ite	le
	addle	r2, sp, #16
	addgt	r2, sp, #8
	cmp	r0, #0
	it	lt
	rsblt	r0, r0, #0
	ldr	r3, [r2, #4]
	add	r3, r3, r0, lsl #20
	str	r3, [r2, #4]
	vldr.64	d6, [sp, #8]
	vldr.64	d7, [sp, #16]
	ldr	r3, .L16
	vdiv.f64	d5, d6, d7
	ldr	r2, [r3]
	ldr	r3, [sp, #28]
	eors	r2, r3, r2
	mov	r3, #0
	bne	.L15
	vmov	r0, r1, d5
	add	sp, sp, #32
	@ sp needed
	pop	{r4, r5, r6, r8, r9, pc}
.L15:
	bl	__stack_chk_fail
.L17:
	.align	2
.L16:
	.word	__stack_chk_guard
	.size	selected_pointer_ratio, .-selected_pointer_ratio
	.ident	"GCC: (Ubuntu 12.5.0-9ubuntu1) 12.5.0"
	.section	.note.GNU-stack,"",%progbits
